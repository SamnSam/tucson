using System;
using System.Data.Objects;

namespace Tucson.EFClient
{
    /// <summary>
    /// Provides functionality for access to entities of the given type in 
    /// the context, the ObjectStateManager, and the underlying store.
    /// </summary>
    public interface ISetProvider : IDisposable
    {
        /// <summary>
        /// Returns a ObjectSet instance for access to entities of the given type in 
        /// the context, the ObjectStateManager, and the underlying store.
        /// </summary>
        /// <typeparam name="T">Type of the entity.</typeparam>
        /// <returns>IObjectSet of type T.</returns>
        IObjectSet<T> GetEntitySet<T>() where T : class;
    }
}