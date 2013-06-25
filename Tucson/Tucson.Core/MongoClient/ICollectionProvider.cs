using System;
using MongoDB.Driver;

namespace Tucson.MongoClient
{
    /// <summary>
    /// Provides functionality for access to documents of the given type in 
    /// the underlying store.
    /// </summary>
    public interface ICollectionProvider : IDisposable
    {
        /// <summary>
        /// Gets or sets the timeout for the collection
        /// </summary>
        TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets a value indicating the number of server instances for the connection
        /// </summary>
        int ServerInstances { get; }

        /// <summary>
        /// Returns a MongoCollection instance for access to documents of the 
        /// given type.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity.</typeparam>
		/// <param name="collectionName">The name of the collection.</param>
        /// <returns>MongoCollection of type <typeparamref name="TEntity"/>.</returns>
        MongoCollection<TEntity> GetCollection<TEntity>(string collectionName) where TEntity : class;

        /// <summary>
        /// Returns a MongoCollection instance for access to documents of the 
        /// given type.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity.</typeparam>
		/// <param name="collectionName">The name of the collection.</param>
		/// <param name="options">Options for creating this collection.</param>
        /// <returns>MongoCollection of type <typeparamref name="TEntity"/>.</returns>
        MongoCollection<TEntity> GetCollection<TEntity>(string collectionName, IMongoCollectionOptions options) 
            where TEntity : class;

		/// <summary>
		/// Returns a MongoCollection instance for access to documents of the 
		/// given type.
		/// </summary>
		/// <typeparam name="TEntity">Type of the entity.</typeparam>
		/// <param name="collectionName">The name of the collection.</param>
		/// <param name="options">Options for creating this collection.</param>
		/// <param name="readMode">The read mode preference</param>
		/// <returns>MongoCollection of type <typeparamref name="TEntity"/>.</returns>
		MongoCollection<TEntity> GetCollection<TEntity>(string collectionName, IMongoCollectionOptions options, ReadPreferenceMode? readMode)
			where TEntity : class;
    }
}
