using System;

namespace Tucson.MongoClient
{
    /// <summary>
    /// Factory class for generating new MongoDB Database objects.
    /// </summary>
    /// <typeparam name="T">Database type</typeparam>
    public class DatabaseFactory<T>: DisposableBase, IDatabaseFactory
        where T : class, ICollectionProvider
    {
        private readonly Func<ICollectionProvider> _creator;

        /// <summary>
        /// Instantiates a new DatabaseFactory for the connection string.
        /// </summary>
        /// <param name="connectionString">Connection string for the database.</param>
        public DatabaseFactory(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString))
                throw new ArgumentException(
                    "DatabaseFactory cannot be instantiated with a null or empty connection string");

            _creator = () => CreateDatabase(connectionString);
        }

        /// <summary>
        /// Instantiates a new DatabaseFactory using a creator method
        /// </summary>
        /// <param name="creator">Delegate to create an instance of the database.</param>
        public DatabaseFactory(Func<ICollectionProvider> creator)
        {
            if (creator == null)
                throw new ArgumentException("DatabaseFactory cannot be instantiated with a null creator");
            _creator = creator;
        }

        /// <summary>
        /// Creates a new ICollectionProvider object.
        /// </summary>
        /// <returns>New Database object.</returns>
        public ICollectionProvider GetDatabase()
        {
            return _creator();
        }

        /// <summary>
        /// Disposes of any objects created solely for use by the DatabaseFactory (GetDatabase() results are NOT disposed)
        /// </summary>
        /// <param name="disposing">True when user is calling Dispose(), false if coming from GC</param>
        protected override void Dispose(bool disposing)
        {
            // do nothing
        }

        private static ICollectionProvider CreateDatabase(string connectionString)
        {
            return (T)Activator.CreateInstance(
                typeof(T),
                new object[] { connectionString },
                new object[0]);
        }
    }
}