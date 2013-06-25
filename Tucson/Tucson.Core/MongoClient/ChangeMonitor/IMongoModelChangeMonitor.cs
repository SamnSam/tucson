using System;
using System.Collections.Generic;
using System.Reflection;
using MongoDB.Driver.Builders;

namespace Tucson.MongoClient.ChangeMonitor
{
    /// <summary>
    /// Interface for tracking changes for mongo entities
    /// </summary>
	internal interface IMongoModelChangeMonitor : IDisposable
    {
        /// <summary>
        /// Clears the list of changes
        /// </summary>
        void ClearChanges();

        /// <summary>
        /// Returns a value indicating whether there are any dirty values
        /// </summary>
        bool HasChanges { get; }

        /// <summary>
        /// Returns a list of Child change monitors
        /// </summary>
        List<IMongoModelChilldPropertyChangeMonitor> ChildChangeMonitors { get; }

        /// <summary>
        /// Gets the object that is proxied
        /// </summary>
        object ProxyInstance { get; }

        /// <summary>
        /// Sets the UpdateBuilder with changes made
        /// </summary>
        /// <param name="parentName">Name of the parent element (when there's no parent, use NULL)</param>
        /// <param name="updateBuilder">The mongo update builder</param>
        void SetChanges(string parentName, UpdateBuilder updateBuilder);
    }

    /// <summary>
    /// Interface for tracking property changes for mongo entities
    /// </summary>
	internal interface IMongoModelPropertyChangeMonitor : IMongoModelChangeMonitor
    {
        /// <summary>
        /// Returns the list of dirty properties for the object
        /// </summary>
        IEnumerable<string> DirtyProperties { get; }
    }

    /// <summary>
    /// Interface for a mongo model embedded property
    /// </summary>
    internal interface IMongoModelChilldPropertyChangeMonitor
    {
        /// <summary>
        /// Gets the property that is monitored
        /// </summary>
        PropInfoGetSet PropInfo { get; }

        /// <summary>
        /// Gets the monitor imnplementation for the property
        /// </summary>
        IMongoModelChangeMonitor Monitor { get; }
    }
}
