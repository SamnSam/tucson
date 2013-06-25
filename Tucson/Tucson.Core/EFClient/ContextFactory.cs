using System;

namespace Tucson.EFClient
{
    /// <summary>
    /// Factory class for generating new Entity Framework DbContext objects.
    /// </summary>
    /// <typeparam name="T">Context type</typeparam>
    public class ContextFactory<T> : IContextFactory
        where T : IContext
    {
        private readonly string _connectionString, _defaultContainerName;
        private readonly Func<string, string, IContext> _creator; 
        private IContext _context;

        /// <summary>
        /// Instantiates a new ContextFactory for the connection string.  The Type T must have a public constructor that takes a connection string
        /// </summary>
        /// <param name="connectionString">Connection string for the context.</param>
        /// <param name="defaultContainerName">The default container name (usually the Entity Container Name in your EDMX file)</param>
        public ContextFactory(string connectionString, string defaultContainerName)
            : this(connectionString, defaultContainerName, CreateContext)
        {
        }

        /// <summary>
        /// Instantiates a new ContextFactory for the connection string using an activator method.
        /// </summary>
        /// <param name="connectionString">Connection string for the context.</param>
        /// <param name="defaultContainerName">The default container name (usually the Entity Container Name in your EDMX file)</param>
        /// <param name="activator">A creator method for the context</param>
        public ContextFactory(string connectionString, string defaultContainerName, Func<string, string, IContext> activator)
        {
            if (String.IsNullOrEmpty(connectionString))
                throw new ArgumentException(
                    "ContextFactory be instantiated with a null or empty connection string");

            _connectionString = connectionString;
            _defaultContainerName = defaultContainerName;
            _creator = activator;
        }

        /// <summary>
        /// Instantiates a new ContextFactory for the connection string using an activator method.
        /// </summary>
        /// <param name="activator">A creator method for the context</param>
        public ContextFactory(Func<IContext> activator)
        {
            _creator = (s1,s2) => activator();
        }

        /// <summary>
        /// Disposes of class instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a new IContext object.
        /// </summary>
        /// <returns>New Context object.</returns>
        public IContext GetContext()
        {
            return _context ?? (_context = _creator(_connectionString, _defaultContainerName));
        }

        private static IContext CreateContext(string connectionString, string defaultContainerName)
        {
            return (T) Activator.CreateInstance(
                typeof (T),
                new object[] {connectionString, defaultContainerName},
                new object[0]);
        }

        /// <summary>
        /// Disposes the context factory.
        /// </summary>
        /// <param name="disposing">Disposing status.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_context != null)
                {
                    var c = _context;
                    _context = null;
                    c.Dispose();
                }
            }
        }
    }
}