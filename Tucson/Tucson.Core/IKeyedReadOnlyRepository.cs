namespace Tucson
{
    /// <summary>
    /// Generic interface for implementing the Repository pattern over
    /// business entities.
    /// </summary>
    /// <typeparam name="TKey">Key type on the entity.</typeparam>
    /// <typeparam name="TEntity">Entity to expose via the repository.</typeparam>
    public interface IKeyedReadOnlyRepository<TKey, TEntity> : IReadOnlyRepository<TEntity>
        where TEntity : class, IKeyedEntity<TKey>
    {
        /// <summary>
        /// Finds an entity based on an expression.
        /// </summary>
        /// <param name="entityId">Entity key.</param>
        /// <returns><typeparamref name="TEntity"/></returns>
        TEntity FindBy(TKey entityId);
    }
}