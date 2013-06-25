using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Objects;
using System.Data.Objects.DataClasses;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Security;

namespace Tucson.EFClient
{
    /// <summary>
    /// Base class for implementing the read only repository pattern over an Entity Framework based entity.
    /// </summary>
    /// <remarks>Derive from this class to create an entity-specific repository.</remarks>
    /// <typeparam name="TEntity">Entity to expose via the repository.</typeparam>
    public class ReadOnlyRepository<TEntity> : DisposableBase, IReadOnlyRepository<TEntity>
        where TEntity : class
    {
        private string _entitySetName;
        private IObjectSet<TEntity> _query;
        private string _repositoryType;
    	private Tuple<bool, DbTransaction> _uncomittedReadState;

        protected string RepositoryType
        {
            get { return _repositoryType ?? (_repositoryType = GetType().FullName); }
        }

        /// <summary>
        /// The KeyName for all entities
        /// </summary>
        protected const string KeyName = "EntityId";

        /// <summary>
        /// Instantiates a new Repository and database context for the entity.
        /// </summary>
        /// <param name="contextFactory">IContextFactory for the database.</param>
        public ReadOnlyRepository(IContextFactory contextFactory)
        {
            ContextFactory = contextFactory;
            Context = ContextFactory.GetContext();
        }

        /// <summary>
        /// Gets or sets a timeout for the repository (note: EF smallest timeout interval is seconds)
        /// </summary>
        public TimeSpan? Timeout
        {
            get
            {
                var ct = ObjectContext.CommandTimeout;
                return ct == null
                           ? (TimeSpan?) null
                           : TimeSpan.FromSeconds(ct.Value);
            }

            set
            {
                if (value.HasValue)
                    ObjectContext.CommandTimeout = (int)value.Value.TotalSeconds;
                else
                    ObjectContext.CommandTimeout = null;
            }
        }

    	/// <summary>
    	/// Gets or sets a flag to default to reading all data using uncommitted reads
    	/// </summary>
    	public AllowReadPreference ReadPreference
    	{
    		get
    		{
    			return _uncomittedReadState == null
    			       	? AllowReadPreference.MustBeCommitted
    			       	: AllowReadPreference.DirtyOk;
    		}
    		set { ToggleAllowDirtyReads(value); }
    	}

    	/// <summary>
        /// This property can be used by derived classes to access the
        /// underlying DbContext object.
        /// </summary>
        protected IContext Context { get; private set; }

        /// <summary>
        /// This property can be used by derived classes to access the
        /// underlying DbContext object.
        /// </summary>
        protected ObjectContext ObjectContext
        {
            get
            {
                var oc = Context as ObjectContext;
                if (oc == null)
                    throw new ApplicationException("Unable to cast Context into ObjectContext");
                return oc;
            }
        }

		internal ObjectContext GetObjectContext()
		{
			return ObjectContext;
		}

        /// <summary>
        /// This property can be used by derived classes to access the
        /// underlying ContextFactory object.
        /// </summary>
        protected IContextFactory ContextFactory { get; private set; }

        /// <summary>
        /// Gets the EntitySetName, required for FindBy and other ObjectContext queries.  Note that DefaultContainerName must be specified during the constructor
        /// </summary>
        protected string EntitySetName
        {
            get
            {
                if (_entitySetName != null)
                    return _entitySetName;

                var dcn = ObjectContext.DefaultContainerName;
                if (string.IsNullOrEmpty(dcn))
                    throw new ApplicationException("Context does not set a default container name.  Please ensure you are setting the default container during the constructor.");

                var esn = ((ObjectSet<TEntity>) Query).EntitySet.Name;

                return _entitySetName = string.Format("{0}.{1}", dcn, esn);
            }
        }

        /// <summary>
        /// This property can be used by derived classes to access the
        /// underlying ObjectSet.
        /// </summary>
        protected IObjectSet<TEntity> Query
        {
            // note: use lazy constructing.  Not all TEntity repositories will be entities that are querable (eg: stored procedures)
			get { return _query ?? (_query = Context.GetEntitySet<TEntity>()); }
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
		/// Loads a dependent property of an instance
		/// </summary>
		/// <param name="instance">The instance</param>
		/// <param name="selector">An expression that returns the property to load</param>
		public void LoadProperty(TEntity instance, Expression<Func<TEntity,object>> selector)
		{
			ObjectContext.LoadProperty(instance, selector);
		}

        /// <summary>
        /// Returns the underlying ObjectSet.
        /// </summary>
        public virtual IQueryable<TEntity> All()
        {
            return Query.AsQueryable();
        }

        /// <summary>
        /// Gets the type of the element(s) that are returned when the
        /// expression tree associated with this repository is executed.
        /// </summary>
        public virtual Type ElementType
        {
            get { return Query.ElementType; }
        }

        /// <summary>
        /// Filters a sequence of entities based on a predicate.
        /// </summary>
        /// <param name="expression">Expression for filtering entity set.</param>
        /// <returns>IQueryable.</returns>
        public virtual IQueryable<TEntity> FilterBy(Expression<Func<TEntity, bool>> expression)
        {
            return Query.Where(expression);
        }

        /// <summary>
        /// Finds an entity based on an expression.
        /// </summary>
        /// <param name="expression">Expression for filtering entity set.</param>
        /// <returns><typeparamref name="TEntity"/></returns>
        public virtual TEntity FindBy(Expression<Func<TEntity, bool>> expression)
        {
        	return TucsonTimings.PerformTimedOperation(
        		RepositoryType,
        		TucsonTimings.GetMethodNameFromStack(1),
        		() => FilterBy(expression).FirstOrDefault())
        		.Item1;
        }

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
		/// <param name="queryName">Name of the query for logging purposes</param>
		/// <param name="query"></param>
		/// <returns></returns>
		public ICollection<T> PerformQuery<T>(string queryName, Func<IQueryable<T>> query)
		{
			return PerformQueryForItem(queryName, () => query().ToList());
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
		/// <param name="queryName">Name of the query for logging purposes</param>
		/// <param name="query">The query to perform</param>
		/// <returns>The result of the query</returns>
		public T PerformQueryForItem<T>(string queryName, Func<T> query)
		{
			return TucsonTimings.PerformTimedOperation(
				RepositoryType,
				TucsonTimings.GetMethodNameFromStack(1),
				() => RetryAction(query))
				.Item1;
		}

		/// <summary>
		/// retries an action up to 3 times
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="action"></param>
		/// <param name="retryMax"></param>
		/// <returns></returns>
		private T RetryAction<T>(Func<T> action, int retryMax = 3)
		{
			for (var tryCounter = 1; ; tryCounter++ )
			{
				try
				{
					return action();
				}
				catch (Exception)
				{
					if (tryCounter >= retryMax)
						throw;
				}
			}
		}

    	/// <summary>
        /// Disposes of the Context and ContextFactory when disposing == true
        /// </summary>
        /// <param name="disposing">True when user is calling Dispose(), false if coming from GC</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
				ToggleAllowDirtyReads(AllowReadPreference.MustBeCommitted);

                // dispose the context/factory
                if (Context != null)
                    Context.Dispose();

                if (ContextFactory != null)
                    ContextFactory.Dispose();
            }
        }


		private void StartUnCommittedRead()
		{
			if (_uncomittedReadState != null)
				return;

			var ctx = ObjectContext;

			bool closeit = false;
			if (ctx.Connection.State != ConnectionState.Open)
			{
				closeit = true;
				ctx.Connection.Open();
			}

			var dbTrans = ctx.Connection.BeginTransaction(IsolationLevel.ReadUncommitted);
			_uncomittedReadState = new Tuple<bool, DbTransaction>(closeit, dbTrans);
		}

		private void StopUnCommittedRead()
		{
			if (_uncomittedReadState == null)
				return;

			var closeit = _uncomittedReadState.Item1;
			var dbTrans = _uncomittedReadState.Item2;

			_uncomittedReadState = null;

			if (dbTrans != null)
				dbTrans.Rollback();
			if (closeit)
				ObjectContext.Connection.Close();
		}

		private void ToggleAllowDirtyReads(AllowReadPreference value)
		{
			if (value == AllowReadPreference.Default)
				value = AllowReadPreference.MustBeCommitted;

			if (value == ReadPreference)
				return;

			if (value == AllowReadPreference.DirtyOk)
				StartUnCommittedRead();
			else
				StopUnCommittedRead();
		}
    }
}