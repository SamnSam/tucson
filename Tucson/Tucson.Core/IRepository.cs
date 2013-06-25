using System.Collections.Generic;

namespace Tucson
{
	/// <summary>
	/// Generic interface for implementing the Repository pattern over
	/// business entities.
	/// </summary>
	/// <typeparam name="TEntity">Entity to expose via the repository.</typeparam>
	public interface IRepository<TEntity> : IReadOnlyRepository<TEntity>
		where TEntity : class
	{
		/// <summary>
		/// Gets a value indicating whether this repository supports Asynchronous updates
		/// </summary>
		bool AsyncSupported { get; }

		/// <summary>
		/// Gets or sets a value indicating whether to perform property change monitoring (True by default for all Entity Framework objects)
		/// </summary>
		bool EnablePropertyChangeMonitor { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether to perform auto commit
		/// </summary>
		bool AutoCommit { get; set; }

		/// <summary>
		/// Adds an entity to the entity set.
		/// </summary>
		/// <param name="entity">Entity object to add.</param>
		void Add(TEntity entity);

		/// <summary>
		/// Adds a collection of entities to the entity set.
		/// </summary>
		/// <param name="entities">A collection of entities of type <typeparamref name="TEntity"/>.</param>
		void Add(IEnumerable<TEntity> entities);

		/// <summary>
		/// Saves all changes made in this context to the underlying database.
		/// </summary>
		void Commit();

		/// <summary>
		/// Removes an entity from the collection.
		/// </summary>
		/// <param name="entity">Entity object to remove.</param>
		void Delete(TEntity entity);

		/// <summary>
		/// Removes a collection of entities from the entity set.
		/// </summary>
		/// <param name="entities">A collection of entities of type <typeparamref name="TEntity"/>.</param>
		void Delete(IEnumerable<TEntity> entities);

		/// <summary>
		/// Updates the given entity in the collection.
		/// </summary>
		/// <param name="entity">Entity to update in the collection.</param>
		void Update(TEntity entity);

		/// <summary>
		/// Adds an entity to the entity set without waiting for the add to succeed or fail
		/// </summary>
		/// <param name="entity">Entity object to add.</param>
		void AsyncAdd(TEntity entity);

		/// <summary>
		/// Updates the given entity in the collection without waiting for the update to succeed or fail
		/// </summary>
		/// <param name="entity">Entity to update in the collection.</param>
		void AsyncUpdate(TEntity entity);

		/// <summary>
		/// Deletes the given entity in the collection without waiting for the delete to succeed or fail
		/// </summary>
		/// <param name="entity">Entity to delete from the collection.</param>
		void AsyncDelete(TEntity entity);
	}
}
