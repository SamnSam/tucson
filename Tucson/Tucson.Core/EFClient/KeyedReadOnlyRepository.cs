using System.Data;
using System.Data.Objects;

namespace Tucson.EFClient
{
    /// <summary>
    /// Base class for implementing the keyed read only repository pattern over an Entity Framework based entity.
    /// </summary>
    /// <remarks>Derive from this class to create an entity-specific repository.</remarks>
    /// <typeparam name="TKey">The type of the key for the entity.</typeparam>
    /// <typeparam name="TEntity">Entity to expose via the repository.</typeparam>
    public class KeyedReadOnlyRepository<TKey, TEntity> : ReadOnlyRepository<TEntity>, IKeyedReadOnlyRepository<TKey, TEntity>
        where TEntity : class, IKeyedEntity<TKey>
    {
        /// <summary>
        /// Instantiates a new Keyed ReadOnly Repository and database context for the entity.
        /// </summary>
        /// <param name="contextFactory">IContextFactory for the database.</param>
        public KeyedReadOnlyRepository(IContextFactory contextFactory) : base(contextFactory)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether to perform property change monitoring (True by default for all Entity Framework objects)
        /// </summary>
        public bool EnablePropertyChangeMonitor
        {
            get { return true; }
            set { }
        }

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
    }
}
