using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Tucson.MongoClient
{
	/// <summary>
	/// Base class for implementing the Repository pattern over a MongoDB based entity.
	/// </summary>
	/// <remarks>Derive from this class to create an entity-specific repository.</remarks>
	/// <typeparam name="TKey">Entity's key type to expose via the repository.</typeparam>
	/// <typeparam name="TEntity">Entity to expose via the repository.</typeparam>
	public class KeyedReadOnlyRepository<TKey, TEntity> : DisposableBase, IKeyedReadOnlyRepository<TKey, TEntity>
		where TEntity : class, IKeyedEntity<TKey>
	{
		//removed beacuase change monitor is not opensource
		//private readonly Lazy<IMongoChangeProxyCreator> _changeProxyCreator = new Lazy<IMongoChangeProxyCreator>(MongoChangeProxyFactory.CreateChangeProxyCreator);
		private readonly string _collectionName;
		private ICollectionProvider _database;
		private Lazy<MongoCollection<TEntity>> _queryRead;
		private Lazy<MongoCollection<TEntity>> _queryWrite;
		private Lazy<MongoCollection<TEntity>> _queryDefault;
		private string _repositoryType;

        protected string RepositoryType
        {
            get { return _repositoryType ?? (_repositoryType = GetType().FullName); }
        }

		/// <summary>
		/// Instantiates a new Repository and collection for the entity.
		/// </summary>
		/// <param name="databaseFactory">IDatabaseFactory for the database.</param>
		/// <param name="collectionName">Name of the collection.</param>
		public KeyedReadOnlyRepository(IDatabaseFactory databaseFactory, string collectionName)
		{
			ReadPreference = AllowReadPreference.Default;
			DatabaseFactory = databaseFactory;
			_collectionName = collectionName;
			SetQuery();
		}

		/// <summary>
		/// Gets or sets a timeout for the repository
		/// </summary>
		public TimeSpan? Timeout
		{
			get
			{
				return Database.Timeout;
			}

			set
			{
				Database.Timeout = value;
				SetQuery();
			}
		}

		/// <summary>
		/// Gets or sets a flag to default to reading all data using uncommitted reads
		/// </summary>
		public AllowReadPreference ReadPreference { get; set; }

		/// <summary>
		/// This property can be used by derived classes to access the
		/// underlying Database object.
		/// </summary>
		protected ICollectionProvider Database
		{
			get
			{
				return _database ?? (_database = DatabaseFactory.GetDatabase());
			}
		}

		/// <summary>
		/// This property can be used by derived classes to access the
		/// underlying DatabaseFactory object.
		/// </summary>
		protected IDatabaseFactory DatabaseFactory { get; private set; }

		/// <summary>
		/// This property can be used by derived classes to access the
		/// underlying MongoCollection.
		/// </summary>
		protected MongoCollection<TEntity> Query
		{
			get
			{
				switch (ReadPreference)
				{
					case AllowReadPreference.DirtyOk:
						return SecondaryReadQuery;
					case AllowReadPreference.MustBeCommitted:
						return WriteQuery;
					// case AllowReadPreference.Default:
					default:
						return DefaultQuery;
				}
			}
		}

		/// <summary>
		/// This property can be used by derived classes to access the
		/// underlying MongoCollection used for committed reading or writing.
		/// eg: ReadPreferenceMode.PrimaryPreferred
		/// </summary>
		protected MongoCollection<TEntity> WriteQuery
		{
			get
			{
				return _queryWrite.Value;
			}
		}

		/// <summary>
		/// This property can be used by derived classes to access the
		/// underlying MongoCollection used for uncommitted reading.
		/// eg: ReadPreferenceMode.SecondaryPreferred
		/// </summary>
		protected MongoCollection<TEntity> SecondaryReadQuery
		{
			get
			{
				return _queryRead.Value;
			}
		}

		/// <summary>
		/// This property can be used by derived classes to access the
		/// underlying MongoCollection used for reading/writing based on the connection string being used by the app.
		/// </summary>
		protected MongoCollection<TEntity> DefaultQuery
		{
			get
			{
				return _queryDefault.Value;
			}
		}


		internal MongoCollection<TEntity> GetQuery()
		{
			return Query;
		}

		/// <summary>
		/// Sends a message to splunk, including the time.
		/// </summary>
		/// <param name="source">The source</param>
		/// <param name="method"></param>
		/// <param name="timer"></param>
		[Obsolete("Use TucsonTimings.SendToSplunk instead")]
        static public void SendToSplunk(string source, string method, Stopwatch timer)
        {
			TucsonTimings.SendToSplunk(source, method, timer);
        }

		/// <summary>
		/// Gets a value indicating whether this repository supports Asynchronous updates
		/// </summary>
		public bool AsyncSupported
		{
			get { return true; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether to perform property change monitoring
		/// </summary>
		public bool EnablePropertyChangeMonitor
		{
			get; set;
		}

		/// <summary>
		/// Returns the underlying collection.  Be sure to use AttachChangeMonitor if you are going to be updating any of the objects from the result.
		/// </summary>
		public virtual IQueryable<TEntity> All()
		{
			return Query.AsQueryable();
		}

		/// <summary>
		/// Filters a sequence of entities based on a predicate.  Be sure to use AttachChangeMonitor if you are going to be updating any of the objects from the result.
		/// </summary>
		/// <param name="expression">Expression for filtering entity set.</param>
		/// <returns>IQueryable.</returns>
		public IQueryable<TEntity> FilterBy(Expression<Func<TEntity, bool>> expression)
		{
			try
			{
				return All().Where(expression);
			}
			catch (Exception e)
			{
				throw new MongoException(e.Message, e);
			}
		}

		/// <summary>
		/// Finds an entity based on an expression.
		/// </summary>
		/// <param name="expression">Expression for filtering entity set.</param>
		/// <returns><typeparamref name="TEntity"/>.</returns>
		public TEntity FindBy(Expression<Func<TEntity, bool>> expression)
		{
			return TucsonTimings.PerformTimedOperation(
				RepositoryType,
				TucsonTimings.GetMethodNameFromStack(1),
				() =>
					{
						var entity = ActOnRepository(
							() =>
								{
									var mongoQueryable = (MongoQueryable<TEntity>)Query.AsQueryable()
										.Where(expression);

									return Query.FindOne(mongoQueryable.GetMongoQuery());
								},
							true);

						return EnablePropertyChangeMonitor
							? AttachChangeMonitor(entity)
							: entity;
					})
				.Item1;
		}

		/// <summary>
		/// Filters a sequence of entities based on the unique identifier.
		/// </summary>
		/// <param name="entityId">Id of the entity.</param>
		/// <returns>T.</returns>
		public TEntity FindBy(TKey entityId)
		{
			return TucsonTimings.PerformTimedOperation(
				RepositoryType,
				TucsonTimings.GetMethodNameFromStack(1),
				() =>
					{
						try
						{

							var entity = ActOnRepository(() => Query.FindOneById(BsonValue.Create(entityId)), true);

							return EnablePropertyChangeMonitor
							       	? AttachChangeMonitor(entity)
							       	: entity;
						}
						catch (Exception e)
						{
							throw new MongoException(e.Message, e);
						}
					})
				.Item1;
		}

		/// <summary>
		/// Attaches an enumeration of entities to the change monitor system for better mongo updates
		/// </summary>
		/// <param name="list">The list of items to attach</param>
		/// <returns>The list of items attached</returns>
		public IEnumerable<TEntity> AttachChangeMonitor(IEnumerable<TEntity> list)
		{
			return list.Select(AttachChangeMonitor);
		}

		/// <summary>
		/// Attaches an entities to the change monitor system for better mongo updates
		/// </summary>
		/// <param name="entity">The entity to attach</param>
		/// <returns>The list of items attached</returns>
		public TEntity AttachChangeMonitor(TEntity entity)
		{
			if (entity == null)
				return null;
			//removed because changeMonitor is not opensource
			return null;//_changeProxyCreator.Value.AttachChangeMonitor(entity);
		}


		//removed because changeMonitor is not opensource
		/// <summary>
		/// Returns the IPropertyChangeMonitor for an instance of a type
		/// </summary>
		/// <param name="proxy">The entity object</param>
		/// <returns>The property change monitor, or NULL if one is not found</returns>
		//internal IMongoModelChangeMonitor GetPropertyChangeMonitor<T>(T proxy)
		//{
		//	if (!EnablePropertyChangeMonitor || !_changeProxyCreator.IsValueCreated)
		//		return null;

		//	return _changeProxyCreator.Value.GetPropertyChangeMonitor(proxy);
		//}

		/// <summary>
		/// Disposes of the Database and DatabaseFactory when disposing == true
		/// </summary>
		/// <param name="disposing">True when user is calling Dispose(), false if coming from GC</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_queryRead = null;
				_queryWrite = null;

				//removed because changeMonitor is not opensource
				// clear the proxy map to help avoid circular references
				//if (_changeProxyCreator != null)
				//	_changeProxyCreator.Value.Dispose();

				if (_database != null)
				{
					var d = _database;
					_database = null;
					d.Dispose();
				}


				if (DatabaseFactory != null)
				{
					var df = DatabaseFactory;
					DatabaseFactory = null;
					df.Dispose();
				}
			}
		}

		private void SetQuery()
		{
			_queryRead = new Lazy<MongoCollection<TEntity>>(() => Database.GetCollection<TEntity>(_collectionName, null, ReadPreferenceMode.SecondaryPreferred));
			_queryWrite = new Lazy<MongoCollection<TEntity>>(() => Database.GetCollection<TEntity>(_collectionName, null, ReadPreferenceMode.Primary));
			_queryDefault = new Lazy<MongoCollection<TEntity>>(() => Database.GetCollection<TEntity>(_collectionName, null, null));
		}

		#region Retry Logic

        /// <summary>
        /// Wraps evaluation of IQueryable in retry logic
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public ICollection<T> PerformQuery<T>(Func<IQueryable<T>> query)
        {
			return PerformQuery(TucsonTimings.GetMethodNameFromStack(1), query);
        }

		/// <summary>
		/// Wraps evaluation of IQueryable in retry logic
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="query"></param>
		/// <returns></returns>
		public ICollection<T> PerformQuery<T>(Func<KeyedReadOnlyRepository<TKey, TEntity>, IQueryable<T>> query)
		{
			return PerformQuery(TucsonTimings.GetMethodNameFromStack(1), query);
		}

		/// <summary>
		/// Wraps evaluation of IQueryable in retry logic
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="queryName">Name of the query for logging purposes</param>
		/// <param name="query"></param>
		/// <returns></returns>
		public ICollection<T> PerformQuery<T>(string queryName, Func<IQueryable<T>> query)
		{
			return PerformQueryForItem(queryName, () => query().ToList());
		}

		/// <summary>
		/// Wraps evaluation of IQueryable in retry logic
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="queryName">Name of the query for logging purposes</param>
		/// <param name="query"></param>
		/// <returns></returns>
		public ICollection<T> PerformQuery<T>(string queryName, Func<KeyedReadOnlyRepository<TKey, TEntity>, IQueryable<T>> query)
		{
			return PerformQueryForItem(queryName, r => query(r).ToList());
		}

		/// <summary>
		/// Wraps evaluation of IQueryable that returns an instance (not a list) in retry logic
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="query">The query to perform</param>
		/// <returns>The result of the query</returns>
		public T PerformQueryForItem<T>(Func<T> query)
		{
			return PerformQueryForItem(TucsonTimings.GetMethodNameFromStack(1), query);
		}

		/// <summary>
		/// Wraps evaluation of IQueryable that returns an instance (not a list) in retry logic
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="query">The query to perform</param>
		/// <returns>The result of the query</returns>
		public T PerformQueryForItem<T>(Func<KeyedReadOnlyRepository<TKey, TEntity>, T> query)
		{
			return PerformQueryForItem(TucsonTimings.GetMethodNameFromStack(1), query);
		}

		/// <summary>
		/// Wraps evaluation of IQueryable that returns an instance (not a list) in retry logic
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="queryName">Name of the query for logging purposes</param>
		/// <param name="query">The query to perform</param>
		/// <returns>The result of the query</returns>
		public T PerformQueryForItem<T>(string queryName, Func<T> query)
		{
			return TucsonTimings.PerformTimedOperation(
				RepositoryType,
				queryName,
				() => ActOnRepository(query, true))
				.Item1;
		}

		/// <summary>
		/// Wraps evaluation of IQueryable that returns an instance (not a list) in retry logic
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="queryName">Name of the query for logging purposes</param>
		/// <param name="query">The query to perform</param>
		/// <returns>The result of the query</returns>
		public T PerformQueryForItem<T>(string queryName, Func<KeyedReadOnlyRepository<TKey, TEntity>, T> query)
		{
			return TucsonTimings.PerformTimedOperation(
				RepositoryType,
				queryName,
				() => ActOnRepository(() => query(this), true))
				.Item1;
		}

		/// <summary>
        /// Retry logic for write operations
        /// </summary>
        /// <param name="act"></param>
        protected void ActOnRepository(Action act)
        {
            ActOnRepository(() =>
                {
                    act();
                    return 1;
                }, false);
        }

        /// <summary>
        /// Retry Logic
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="act"></param>
        /// <param name="isReadOnlyOperation"></param>
        /// <returns></returns>
        private T ActOnRepository<T>(Func<T> act, bool isReadOnlyOperation)
        {
            var counter = 0;
            var sleepMs = 500;
            const int maxWait = 32000; // .5, 1, 2, 4, 8, 16, 32

            while (true)
            {
                try
                {
                    if (!isReadOnlyOperation)
                        Database.GetCollection<TEntity>(_collectionName);

                    // Try to perform the operation
                    var result = act();

                    return result;
                }
                catch (Exception ex)
                {
                    if (sleepMs >= maxWait)
                    {
                        LogRetryEvent(EnumRetryEventType.TimeoutExceeded);
                        throw;
                    }

                    if (CheckExceptionConditions(ex, IsConnectionPoolRecyclingDueToElection, IsElectionHappening,
                        IsQueryFailure, IsServerNotConnected, IsTimeoutException, IsOtherExpectedError, IsOtherMongoException, IsSlaveOkFalseElection))
                    {
                        LogRetryEvent(EnumRetryEventType.OperationRetried);
                        Thread.Sleep(sleepMs);
                        sleepMs *= 2;
                        counter++;
                        continue;
                    }
                    
                    // If we insert a document at the exact moment we fail over, it looks like an error and we will retry.
                    // However, we'll get a "Duplicate Key" exception since the first insert was successful. If this condition is met,
                    // we recognize that the first insert was successful and return from this method.
                    if (!isReadOnlyOperation && counter > 0 && CheckExceptionConditions(ex, IsDuplicateKeyException))
                    {
                        // Write operations should not be called externally, so it's OK to return null
                        return default(T);
                    }

                    LogRetryEvent(EnumRetryEventType.ErrorUnhandled);
                    throw;
                }
            }
        }

        /// <summary>
        /// Checks an exception to determine whether it was thrown due to a successful add being retried, causing a 
        /// duplicate key error. 
        /// </summary>
		private static bool IsDuplicateKeyException(Exception ex)
        {
        	return (ex is WriteConcernException || ex is MongoSafeModeException) && ex.Message.Contains("\"code\" : 11000");
        }

		/// <summary>
        /// Checks an exception to determine whether it was thrown when the primary node in a replica set is offline or has stepped down 
        /// and the connection pool is being recycled to accomodate this fact.
        /// </summary>
		private static bool IsConnectionPoolRecyclingDueToElection(Exception ex)
		{
			return ex is IOException && ex.InnerException is SocketException &&
			       ((SocketException) ex.InnerException).ErrorCode == 10054;
		}

		/// <summary>
        /// Checks an exception to determine whether it was thrown while there is no primary node in a replica set. If this is
        /// the case, then an election must be occurring. The conditions evaluated by this method will be true only after the 
        /// connection pool has been recycled. 
        /// </summary>
        private static bool IsElectionHappening(Exception ex)
		{
			return ex is MongoConnectionException &&
			       ex.Message.IndexOf("primary", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
        /// Checks an exception to determine whether it was thrown because a query failed.
        /// </summary>
		private static bool IsQueryFailure(Exception ex)
		{
			return ex is MongoQueryException && ex.Message.Contains("\"code\" : 11600");
		}

        /// <summary>
        /// Checks an exception to determine whether it was thrown because a query failed.
        /// </summary>
        private static bool IsSlaveOkFalseElection(Exception ex)
        {
            return ex is MongoQueryException && (ex.Message.Contains("\"code\" : 15988") || ex.Message.Contains("\"code\" : 10009"));
        }

		/// <summary>
	    /// Checks an exception to determine whether it was thrown because a server is no longer connected.
	    /// </summary>
		private static bool IsServerNotConnected(Exception ex)
		{
			return ex is InvalidOperationException && ex.Message.Contains("is no longer connected");
		}

		/// <summary>
        /// Checks an exception to determine whether it matches any "miscellaneous" condition that can occur while there is no
        /// primary node in a replica set. 
        /// </summary>
		private static bool IsOtherExpectedError(Exception ex)
		{
			return ex is EndOfStreamException;
		}

		private static bool IsTimeoutException(Exception ex)
		{
			return ex is IOException &&
			       ex.Message.Contains(
			       	"Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond");
		}

        /// <summary>
        /// Checks an exception to determine whether it was a non-specific Mongo issue. 
        /// </summary>
        private static bool IsOtherMongoException(Exception ex)
        {
            return ex is MongoConnectionException;
        }

		/// <summary>
        /// Evaluates a collection of exception conditions to determine whether any of them have been met. 
        /// If the type of <paramref name="ex"/> is TargetInvocationException, the InnerException property of 
        /// <paramref name="ex"/> will be checked, instead of <paramref name="ex"/> itself.
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="conditions"></param>
        /// <returns></returns>
        private static bool CheckExceptionConditions(Exception ex, params Func<Exception, bool>[] conditions)
        {
			if (conditions == null)
				return false;

            var exception = ex is TargetInvocationException ? ex.InnerException : ex;

			return conditions.Any(condition => condition(exception));
        }

        private enum EnumRetryEventType
        {
            TimeoutExceeded,
            OperationRetried,
            ErrorUnhandled
        }

        private static void LogRetryEvent(EnumRetryEventType retryEventType)
        {
            try
            {
                var counter = string.Format("Tucson.MongoClient.Retry.{0}", retryEventType).ToLower();
                //logger code should go here
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine("Error incrementing log counter: " + ex.Message);
#endif
            }
        }

	    #endregion
	}
}
