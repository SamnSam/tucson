namespace Tucson
{
    /// <summary>
    /// Represents an entity with a key.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public interface IKeyedEntity<TKey>
    {
        /// <summary>
        /// Unique key for the entity.
        /// </summary>
        TKey EntityId { get; set; }
    }
}
