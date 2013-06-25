using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;


namespace Tucson.MongoClient
{
    /// <summary>
    /// Base class for implementing the Repository pattern over a MongoDB based entity.
    /// </summary>
    /// <remarks>Derive from this class to create an entity-specific repository.</remarks>
    /// <typeparam name="TKey">Entity's key type to expose via the repository.</typeparam>
    /// <typeparam name="TEntity">Entity to expose via the repository.  The Entity should implement IUpdateableEntity.</typeparam>
    public class Repository<TKey, TEntity> : KeyedReadOnlyRepository<TKey, TEntity>, IKeyedRepository<TKey, TEntity>
        where TEntity : class, IKeyedEntity<TKey>, IUpdateableEntity
    {
        private List<Action> _pendingActions;

        // ReSharper disable StaticFieldInGenericType

        // NOTE: these two static SafeMode fields are separate per type TKey/TEntity.  But for a given TKey/TEntity,
        //       they will all be the same value.  The MajoritySafeMode fails back to the SingleSafeMode if,
        //       during an add/update, an exception is thrown indicating that Safe mode is not supported (eg: not a replica set)
        //       Since this failback occurs per TKey/TEntity, this allows for some repositories to be a replica set 
        //       and others not.

    	/// <summary>
    	/// majority update - enforces majority update across the replica set before the update/add statement returns
    	/// </summary>
    	private static readonly SafeMode MajoritySafeModeStatic = new SafeMode(true)
    	                                                          	{
    	                                                          		W = 2,
    	                                                          		WTimeout = TimeSpan.FromSeconds(15)
    	                                                          	};

        /// <summary>
        /// single update - enforces update in at least one of the replica set before the update/add statement returns
        /// </summary>
        private static readonly SafeMode SingleSafeModeStatic = new SafeMode(true);

        /// <summary>
        /// No safe mode for Mongo - fire and forget updates 
        /// </summary>
        private static readonly SafeMode AsyncSafeModeStatic = new SafeMode(false);

        // ReSharper restore StaticFieldInGenericType

        /// <summary>
        /// single update - enforces update in at least one of the replica set before the update/add statement returns
        /// </summary>
        private SafeMode _syncSafeMode;

        static Repository()
        {
            SingleSafeModeStatic.Freeze();
            MajoritySafeModeStatic.Freeze();
            AsyncSafeModeStatic.Freeze();
        }

        /// <summary>
        /// Instantiates a new Repository and collection for the entity.
        /// </summary>
        /// <param name="databaseFactory">IDatabaseFactory for the database.</param>
        /// <param name="collectionName">Name of the collection.</param>
        public Repository(IDatabaseFactory databaseFactory, string collectionName)
            : base(databaseFactory, collectionName)
        {
            AutoCommit = false;

            // TODO: re-enable this default in the future
            EnablePropertyChangeMonitor = false;

        	DefaultBatchSize = 100;
        }

        [Obsolete("Index Definitions should no longer be used, as indexes are now going to be handled explicitly by DBAs rather than in code.")]
        public Repository(IDatabaseFactory databaseFactory, string collectionName, Func<IEnumerable<IndexDefinition>> indexDefinitionFunc)
            : this(databaseFactory, collectionName)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether to perform auto commit
        /// </summary>
        public bool AutoCommit { get; set; }

		/// <summary>
		/// The default number of entities to process for batchable processes, like Delete(IEnumerable).  Default: 100
		/// </summary>
		public int DefaultBatchSize { get; set; }

        /// <summary>
        /// Gets the synchronous safe mode for this repository
        /// </summary>
        protected SafeMode SyncSafeMode
        {
            get
            {
                return _syncSafeMode ??
                       (_syncSafeMode = Database.ServerInstances > 1 ? MajoritySafeModeStatic : SingleSafeModeStatic);
            }
        }

        /// <summary>
        /// Gets the asynchronous safe mode for this repository
        /// </summary>
        protected SafeMode AsyncSafeMode
        {
            get
            {
                return AsyncSafeModeStatic;
            }
        }

        /// <summary>
        /// Gets a list of pending actions to perform
        /// </summary>
        private List<Action> PendingActions
        {
            get { return _pendingActions ?? (_pendingActions = new List<Action>()); }
        }

        /// <summary>
        /// Adds the given entity to the collection.
        /// </summary>
        /// <param name="entity">Entity to add to the collection.</param>
        public virtual void Add(TEntity entity)
        {
            Add(entity, SyncSafeMode);
        }

        /// <summary>
        /// Adds the given entities into the database. 
        /// </summary>
        /// <param name="entities">IEnumerable of entities.</param>
        public virtual void Add(IEnumerable<TEntity> entities)
        {
            if (entities != null)
            {
                foreach (var entity in entities)
                    Add(entity);
            }
        }

        /// <summary>
        /// Saves all changes made in this context to the underlying database
        /// </summary>
        public virtual void Commit()
        {
            if (_pendingActions != null)
            {
                _pendingActions.ForEach(ActOnRepository);
                _pendingActions.Clear();
            }
        }

        /// <summary>
        /// Either adds or updates an existing document in mongo by the entity id
        /// </summary>
        /// <param name="entity"></param>
        public void Upsert(TEntity entity)
        {
            Upsert(entity, SyncSafeMode);
        }

        /// <summary>
        /// Removes the given entity from the collection.
        /// </summary>
        /// <param name="entity">Entity to remove from the collection.</param>
        public void Delete(TEntity entity)
        {
            Delete(entity.EntityId, SyncSafeMode);
        }


		//not supported
        /// <summary>
        /// Removes the given entity set from the collection.
        /// </summary>
        /// <param name="entities">IEnumerable of entities.</param>
		/*
        public virtual void Delete(IEnumerable<TEntity> entities)
        {
            if (entities != null)
            {
                Delete(entities.Select(d => d.EntityId));
            }
        }*/


		/// <summary>
		/// Removes the given entity set from the collection.
		/// </summary>
		/// <param name="entities">IEnumerable of entities.</param>
		public virtual void Delete(IEnumerable<TEntity> entities)
		{
			if (entities != null)
			{
				foreach(IEnumerable<TEntity>entity in entities)
				{
					Delete(entity);
				}
			}
		}

        /// <summary>
        /// Removes an entity from the collection.
        /// </summary>
        /// <param name="key">Key for the entity object to remove.</param>
        public void Delete(TKey key)
        {
            Delete(key, SyncSafeMode);
        }

		//not supported
		//- Paginate is not in System.Collections.Generic.IEnumerable<MongoDB.Driver.IMongoQuery> - but you can code it yourself
        /// <summary>
        /// Removes a collection of entities by key from the collection in batches of DefaultBatchSize
        /// </summary>
        /// <param name="keys">A collection of keys of type <typeparamref name="TKey"/>.</param>
		 /*
        public void Delete(IEnumerable<TKey> keys)
        {
        	Delete(keys, 100);
        }*/


		//- Paginate is not in System.Collections.Generic.IEnumerable<MongoDB.Driver.IMongoQuery> - but you can code it yourself
		/// <summary>
		/// Removes a collection of entities from the entity set.
		/// </summary>
		/// <param name="keys">A collection of keys of type <typeparamref name="TKey"/>.</param>
		/// <param name="batchSize">size of each delete batch</param>
		/*
		public long Delete(IEnumerable<TKey> keys, int batchSize)
		{
			long result = 0;

			if (keys != null)
			{
				foreach (var batch in keys
					.Select(key => MongoDB.Driver.Builders.Query.EQ("_id", BsonValue.Create(key)))
					.Paginate(batchSize)) 
				        
				{
					var q = MongoDB.Driver.Builders.Query.Or(batch);
					CommitAction(() =>
					             	{
					             		var r = WriteQuery.Remove(q, SyncSafeMode);
										if (r != null)
											result += r.DocumentsAffected;
					             	});
				}
			}

			return result;
		}*/

        /// <summary>
        /// Deletes the given entity in the collection without waiting for the delete to succeed or fail
        /// </summary>
        /// <param name="key">Key for the entity to delete from the collection.</param>
        public void AsyncDelete(TKey key)
        {
            Delete(key, AsyncSafeMode);
        }

        /// <summary>
        /// Updates the given entity in the collection.
        /// </summary>
        /// <param name="entity">Entity to update in the collection.</param>
        public void Update(TEntity entity)
        {
            Update(entity, SyncSafeMode);
        }

        /// <summary>
        /// Adds an entity to the entity set without waiting for the add to succeed or fail
        /// </summary>
        /// <param name="entity">Entity object to add.</param>
        public void AsyncAdd(TEntity entity)
        {
            Add(entity, AsyncSafeMode);
        }

        /// <summary>
        /// Updates the given entity in the collection with out caring that the updates succeed or not.
        /// </summary>
        /// <param name="entity">Entity to update in the collection.</param>
        public void AsyncUpdate(TEntity entity)
        {
            Update(entity, AsyncSafeMode);
        }

        /// <summary>
        /// Deletes the given entity in the collection without waiting for the delete to succeed or fail
        /// </summary>
        /// <param name="entity">Entity to delete from the collection.</param>
        public void AsyncDelete(TEntity entity)
        {
            Delete(entity.EntityId, AsyncSafeMode);
        }

        /// <summary>
        /// Update a field using a set so the complete document doesn't need to be replaced.
        /// Finds the record to be updated using the entity id
        /// Set value to NULL to unset (remove) the field
        /// </summary>
        /// <param name="entityId">Id to update</param>
        /// <param name="fieldName">mongo name of the field to be update</param>
        /// <param name="value">new value for the field</param>
        public int UpdateFieldInPlace(string entityId, string fieldName, object value)
        {
            var q = MongoDB.Driver.Builders.Query.EQ("_id", BsonValue.Create(entityId));
            UpdateBuilder updateStatement = null;
            if (value == null)
                updateStatement = MongoDB.Driver.Builders.Update.Unset(fieldName);
            else
                updateStatement = MongoDB.Driver.Builders.Update.Set(fieldName, BsonValue.Create(value));
			var result = WriteQuery.Update(q, updateStatement, SyncSafeMode);
            if (result != null)
                return (int)result.DocumentsAffected;

            return 0;
        }

        /// <summary>
        /// Update a field using a set so the complete document doesn't need to be replaced
        /// Finds the record to be updated using the entityId and also the andAlso query
        /// Set value to NULL to unset (remove) a the field
        /// </summary>
        /// <param name="entityId">Id to update</param>
        /// <param name="fieldName">mongo name of the field to be update</param>
        /// <param name="value">new value for the field</param>
        /// <param name="andAlso">Use for find criteria</param>
        public int UpdateFieldInPlace(string entityId, IMongoQuery andAlso, string fieldName, object value)
        {
            var q = MongoDB.Driver.Builders.Query.EQ("_id", BsonValue.Create(entityId));
            q = MongoDB.Driver.Builders.Query.And(q, andAlso);
            UpdateBuilder updateStatement = null;
            if (value == null)
                updateStatement = MongoDB.Driver.Builders.Update.Unset(fieldName);
            else
                updateStatement = MongoDB.Driver.Builders.Update.Set(fieldName, BsonValue.Create(value));
			var result = WriteQuery.Update(q, updateStatement, SyncSafeMode);

            if (result != null)
                return (int)result.DocumentsAffected;

            return 0;
        }

        /// <summary>
        /// Disposes of any pending actions
        /// </summary>
        /// <param name="disposing">True when user is calling Dispose(), false if coming from GC</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_pendingActions != null)
                    _pendingActions.Clear();
            }

            base.Dispose(disposing);
        }


        /// <summary>
        /// Gets or sets the last swallowed SafeModeResult
        /// </summary>
        protected SafeModeResult LastSafeModeResult { get; set; }

        /// <summary>
        /// Swallows a SafeModeResult
        /// </summary>
        /// <param name="smr"></param>
        protected void SwallowSafeModeResult(SafeModeResult smr)
        {
            // should we do something with the result here?
            // http://www.mongodb.org/display/DOCS/CSharp+getLastError+and+SafeMode
            // implies we don't need to do anything
            LastSafeModeResult = smr;
        }

        /// <summary>
        /// Sets the update and create dates for an entity that implements IUpdateableEntity.  Called automatically by Update/Upsert/Add.  Call this only if you are manually updating your entity.
        /// </summary>
        /// <param name="entity">The entity to update</param>
        protected void SetUpdateDate(TEntity entity)
        {
            var ue = entity as IUpdateableEntity;

            if (ue != null)
            {
                ue.UpdateDate = DateTime.UtcNow;
                if (!ue.CreateDate.HasValue)
                    ue.CreateDate = ue.UpdateDate;
            }
        }

        private void Add(TEntity entity, SafeMode safeMode)
        {
            if (ReferenceEquals(entity.EntityId, default(TEntity)))
                entity.EntityId = (TKey)GenerateId(typeof(TKey), entity.EntityId);

            SetUpdateDate(entity);

			CommitAction(() => SwallowSafeModeResult(WriteQuery.Insert<TEntity>(entity, safeMode)));
        }

        private void Delete(TKey key, SafeMode safeMode)
        {
            var q = MongoDB.Driver.Builders.Query.EQ("_id", BsonValue.Create(key));
			CommitAction(() => SwallowSafeModeResult(WriteQuery.Remove(q, safeMode)));
        }

        private void Upsert(TEntity entity, SafeMode safeMode)
        {
          
			Update(entity, UpdateFlags.Upsert, safeMode);
        }

        private void Update(TEntity entity, SafeMode safeMode)
        {
            Update(entity, UpdateFlags.None, safeMode);
        }

	    private void Update(TEntity entity, UpdateFlags flags, SafeMode safeMode)
		{
		    var ub = new UpdateBuilder();

			string ubs;
			if (ub == null || (ubs = ub.ToString()) == "{ }")
				return; // nothing to update

			// something changed, do the update for the id
			SetUpdateDate(entity);

			if (ub is UpdateBuilder && !ubs.Contains("\"ud\""))
			{
				// update date already changed
				var ue = (IUpdateableEntity) entity;
				var updbld = (UpdateBuilder) ub;
				updbld.Set("ud", ue.UpdateDate); // note: assumes that the update date field is called "ud"!
			}

			var idq = MongoDB.Driver.Builders.Query.EQ("_id", BsonValue.Create(entity.EntityId));
			CommitAction(() => SwallowSafeModeResult(WriteQuery.Update(idq, ub, flags, safeMode)));
							
	     }

		//removed because changeMonitor is not opensource
		/*
        private void Update(TEntity entity, UpdateFlags flags, SafeMode safeMode)
        {
            var cm = GetPropertyChangeMonitor(entity);
            var ub = GenerateUpdate(cm, entity);

            string ubs;
            if (ub == null || (ubs = ub.ToString()) == "{ }")
                return; // nothing to update

            // something changed, do the update for the id
            SetUpdateDate(entity);

            if (ub is UpdateBuilder && !ubs.Contains("\"ud\""))
            {
                // update date already changed
                var ue = (IUpdateableEntity) entity;
                var updbld = (UpdateBuilder) ub;
                updbld.Set("ud", ue.UpdateDate); // note: assumes that the update date field is called "ud"!
            }

            var idq = MongoDB.Driver.Builders.Query.EQ("_id", BsonValue.Create(entity.EntityId));
			CommitAction(() => SwallowSafeModeResult(WriteQuery.Update(idq, ub, flags, safeMode)));

            if (cm != null)
                cm.ClearChanges();
        }


        private IMongoUpdate GenerateUpdate(IMongoModelChangeMonitor propMon, object entity)
        {
            if (propMon == null)
                return MongoDB.Driver.Builders.Update.Replace(typeof(TEntity), entity);

            var ub = new UpdateBuilder();
            propMon.SetChanges(null, ub);

            return ub;
        }*/

        private void CommitAction(Action a)
        {
            if (AutoCommit)
                ActOnRepository(a);
            else
            {
                // add it to pending
                PendingActions.Add(a);
            }
        }

        private static object GenerateId(Type keyType, object keyValue)
        {
            if (keyType == typeof(string))
            {
                return ObjectId.NewObjectId().ToString().ToLower();
            }

            if (keyType == typeof(BsonObjectId))
            {
                return BsonObjectId.Create(ObjectId.NewObjectId());
            }

            return keyValue;
        }
    }
}
