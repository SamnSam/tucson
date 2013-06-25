using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Objects.DataClasses;

namespace Tucson.EFClient
{
	/// <summary>
	/// Base class for implementing the Repository pattern over an Entity Framework based entity.
	/// </summary>
	/// <remarks>Derive from this class to create an entity-specific repository.</remarks>
	/// <typeparam name="TEntity">Entity to expose via the repository.</typeparam>
	public class NonKeyedRepository<TEntity> : ReadOnlyRepository<TEntity>, IRepository<TEntity>
		where TEntity : class
	{
		/// <summary>
		/// Instantiates a new non-keyed Repository and database context for the entity.
		/// </summary>
		/// <param name="contextFactory">IContextFactory for the database.</param>
		public NonKeyedRepository(IContextFactory contextFactory) : base(contextFactory)
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
		/// Gets a value indicating whether this repository supports Asynchronous updates
		/// </summary>
		public bool AsyncSupported
		{
			get { return false; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether to perform auto commit
		/// </summary>
		public bool AutoCommit { get; set; }

		/// <summary>
		/// Adds the given entity to the context underlying the set in the 
		/// Added state such that it will be inserted into the database when 
		/// SaveChanges is called.
		/// </summary>
		/// <param name="entity">Entity to add to the context.</param>
		public virtual void Add(TEntity entity)
		{
			Query.AddObject(entity);
			PerformAutoCommit();
		}

		/// <summary>
		/// Adds the given entities to the context underlying the set in the 
		/// Added state such that it will be inserted into the database when 
		/// SaveChanges is called.
		/// </summary>
		/// <param name="entities">IEnumerable of entities.</param>
		public virtual void Add(IEnumerable<TEntity> entities)
		{
			if (entities != null)
			{
				foreach (TEntity entity in entities)
					Query.AddObject(entity);
				PerformAutoCommit();
			}
		}

		/// <summary>
		/// Saves all changes made in this context to the underlying database.
		/// </summary>
		public virtual void Commit()
		{
			Context.Commit();
		}

		/// <summary>
		/// Marks the given entity as Deleted such that it will be deleted from 
		/// the database when SaveChanges is called. The entity must exist in the
		/// context in some other state before this method is called.
		/// </summary>
		/// <param name="entity">Entity to remove from the context.</param>
		public virtual void Delete(TEntity entity)
		{
			Query.DeleteObject(entity);
			PerformAutoCommit();
		}

		/// <summary>
		/// Marks the given entities as Deleted such that it will be deleted from 
		/// the database when SaveChanges is called. The entity must exist in the
		/// context in some other state before this method is called.
		/// </summary>
		/// <param name="entities">IEnumerable of entities.</param>
		public virtual void Delete(IEnumerable<TEntity> entities)
		{
			if (entities != null)
			{
				foreach (TEntity entity in entities)
					Query.DeleteObject(entity);
				PerformAutoCommit();
			}
		}

		/// <summary>
		/// Updates the given entity in the collection.
		/// </summary>
		/// <param name="entity">Entity to update in the collection.</param>
		public virtual void Update(TEntity entity)
		{
			var obj = entity as EntityObject;
			if (obj == null || obj.EntityState == EntityState.Detached)
			{
				try
				{
					Query.Attach(entity);
				}
				catch (InvalidOperationException ex)
				{
					// ignore errors when the object is already attached
					if (!ex.Message.Contains("The object cannot be attached because it is already in the object context."))
						throw;
				}
			}

			PerformAutoCommit();
		}

		/// <summary>
		/// Adds an entity to the entity set without waiting for the add to succeed or fail
		/// </summary>
		/// <param name="entity">Entity object to add.</param>
		public void AsyncAdd(TEntity entity)
		{
			Add(entity);
		}

		/// <summary>
		/// Updates the given entity in the collection without waiting for the add to succeed or fail
		/// </summary>
		/// <param name="entity">Entity to update in the collection.</param>
		public void AsyncUpdate(TEntity entity)
		{
			Update(entity);
		}

		/// <summary>
		/// Deletes the given entity in the collection without waiting for the delete to succeed or fail
		/// </summary>
		/// <param name="entity">Entity to delete from the collection.</param>
		public void AsyncDelete(TEntity entity)
		{
			Delete(entity);
		}

		private void PerformAutoCommit()
		{
			if (AutoCommit)
				Commit();
		}
	}
}
