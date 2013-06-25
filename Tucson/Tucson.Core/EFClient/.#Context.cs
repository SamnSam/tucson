using System.Data.Objects;

namespace Tucson.EFClient
{
    /// <summary>
    /// Represents a combination of the Unit Of Work and Repository patterns 
    /// such that it can be used to query from a database and group together 
    /// changes that will then be written back to the store as a unit.
    /// </summary>
    public class Context : ObjectContext, IContext
    {
        /// <summary>
        /// Constructs a new context
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        /// <param name="defaultContainerName">The default container name (usually the Entity Container Name in your EDMX file)</param>
        public Context(string connectionString, string defaultContainerName)
            : base(connectionString, defaultContainerName)
        {
        }

        /// <summary>
        /// Saves all changes made in this context to the underlying database.
        /// </summary>
        public virtual void Commit()
        {
            base.SaveChanges();
        }

        /// <summary>
        /// Returns a ObjectSet instance for access to entities of the given type in 
        /// the context, the ObjectStateManager, and the underlying store.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity.</typeparam>
        /// <returns>IObjectSet of type TEntity.</returns>
        public virtual IObjectSet<TEntity> GetEntitySet<TEntity>() where TEntity : class
        {
            return base.CreateObjectSet<TEntity>();
        }
    }
}