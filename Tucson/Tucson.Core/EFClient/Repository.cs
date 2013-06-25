using System.Collections.Generic;
using System.Data;
using System.Data.Objects;
using System.Data.Objects.DataClasses;
using System.Linq;

namespace Tucson.EFClient
{
    /// <summary>
    /// Base class for implementing the Repository pattern over an Entity Framework based entity.
    /// </summary>
    /// <remarks>Derive from this class to create an entity-specific repository.</remarks>
    /// <typeparam name="TKey">The type of the key for the entity.</typeparam>
    /// <typeparam name="TEntity">Entity to expose via the repository.</typeparam>
	public class Repository<TKey, TEntity> : NonKeyedRepository<TEntity>, IKeyedRepository<TKey, TEntity>
        where TEntity : class, IKeyedEntity<TKey>
    {
        /// <summary>
        /// Instantiates a new Repository and database context for the entity.
        /// </summary>
        /// <param name="contextFactory">IContextFactory for the database.</param>
        public Repository(IContextFactory contextFactory):base(contextFactory)
        {
            AutoCommit = false;
        }

		#region IKeyedRepository

		/// <summary>
		/// Finds an entity based on an expression.
		/// </summary>
		/// <param name="entityId">Entity key.</param>
		/// <returns>The entity or NULL</returns>
		public virtual TEntity FindBy(TKey entityId)
		{
			return TucsonTimings.PerformTimedOperation(
				RepositoryType,
				TucsonTimings.GetMethodNameFromStack(1),
				() =>
					{
						try
						{
							return (TEntity) ((ObjectContext) Context)
							                 	.GetObjectByKey(new EntityKey(EntitySetName, KeyName, entityId));
						}
						catch (ObjectNotFoundException)
						{
							return null;
						}
					})
				.Item1;
		}

        /// <summary>
        /// Removes an entity from the collection.
        /// </summary>
        /// <param name="key">Key for the entity object to remove.</param>
        public void Delete(TKey key)
        {
            Delete(FindBy(key));
        }

        /// <summary>
        /// Removes a collection of entities from the entity set.
        /// </summary>
        /// <param name="keys">A collection of keys of type <typeparamref name="TKey"/>.</param>
        public void Delete(IEnumerable<TKey> keys)
        {
            if (keys != null)
                Delete(FilterBy(e => keys.Contains(e.EntityId)));
        }

        /// <summary>
        /// Deletes the given entity in the collection without waiting for the delete to succeed or fail
        /// </summary>
        /// <param name="key">Key for the entity to delete from the collection.</param>
        public void AsyncDelete(TKey key)
        {
            Delete(key);
		}
		#endregion

	}
}