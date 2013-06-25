using System.Collections.Generic;

namespace Tucson
{
    /// <summary>
    /// Generic interface for implementing the Repository pattern over
    /// business entities.
    /// </summary>
    /// <typeparam name="TKey">Key type on the entity.</typeparam>
    /// <typeparam name="TEntity">Entity to expose via the repository.</typeparam>
    public interface IKeyedRepository<TKey, TEntity> : IKeyedReadOnlyRepository<TKey, TEntity>, IRepository<TEntity>
        where TEntity : class, IKeyedEntity<TKey>
    {
        /// <summary>
        /// Removes an entity from the collection.
        /// </summary>
        /// <param name="key">Key for the entity object to remove.</param>
        void Delete(TKey key);

        //not supported
		/// <summary>
        /// Removes a collection of entities from the entity set.
        /// </summary>
        /// <param name="keys">A collection of keys of type <typeparamref name="TKey"/>.</param>
        //void Delete(IEnumerable<TKey> keys);

        /// <summary>
        /// Deletes the given entity in the collection without waiting for the delete to succeed or fail
        /// </summary>
        /// <param name="key">Key for the entity to delete from the collection.</param>
        void AsyncDelete(TKey key);
    }
}