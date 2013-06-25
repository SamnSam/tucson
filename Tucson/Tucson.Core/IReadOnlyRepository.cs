using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Tucson
{
    /// <summary>
    /// Generic interface for implementing the Repository pattern over
    /// business entities.
    /// </summary>
    /// <typeparam name="TEntity">Entity to expose via the repository.</typeparam>
	public interface IReadOnlyRepository<TEntity> : IReadOnlyRepository
        where TEntity : class
    {
        /// <summary>
        /// Returns the underlying entity set.
        /// </summary>
        IQueryable<TEntity> All();

        /// <summary>
        /// Filters a sequence of entities based on an expression.
        /// </summary>
        /// <param name="expression">Expression for filtering entity set.</param>
        /// <returns>IQueryable.</returns>
        IQueryable<TEntity> FilterBy(Expression<Func<TEntity, bool>> expression);

        /// <summary>
        /// Finds an entity based on an expression.
        /// </summary>
        /// <param name="expression">Expression for finding an entity.  </param>
        /// <returns><typeparamref name="TEntity"/>.</returns>
        TEntity FindBy(Expression<Func<TEntity, bool>> expression);

    	/// <summary>
    	/// Wraps evaluation of IQueryable in retry logic
    	/// </summary>
    	/// <typeparam name="T"></typeparam>
    	/// <param name="query"></param>
    	/// <returns></returns>
    	ICollection<T> PerformQuery<T>(Func<IQueryable<T>> query);

    	/// <summary>
    	/// Wraps evaluation of IQueryable in retry logic
    	/// </summary>
    	/// <typeparam name="T"></typeparam>
    	/// <param name="queryName">Name of the query for logging purposes</param>
    	/// <param name="query"></param>
    	/// <returns></returns>
    	ICollection<T> PerformQuery<T>(string queryName, Func<IQueryable<T>> query);

    	/// <summary>
    	/// Wraps evaluation of IQueryable that returns an instance (not a list) in retry logic
    	/// </summary>
    	/// <typeparam name="T"></typeparam>
    	/// <param name="query">The query to perform</param>
    	/// <returns>The result of the query</returns>
    	T PerformQueryForItem<T>(Func<T> query);

    	/// <summary>
    	/// Wraps evaluation of IQueryable that returns an instance (not a list) in retry logic
    	/// </summary>
    	/// <typeparam name="T"></typeparam>
    	/// <param name="queryName">Name of the query for logging purposes</param>
    	/// <param name="query">The query to perform</param>
    	/// <returns>The result of the query</returns>
    	T PerformQueryForItem<T>(string queryName, Func<T> query);
    }
}
