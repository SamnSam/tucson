using System;

namespace Tucson.EFClient
{
    /// <summary>
    /// Generic interface for generating new Entity Framework DbContext objects.
    /// </summary>
    public interface IContextFactory : IDisposable
    {
        /// <summary>
        /// Creates a new IContext object.
        /// </summary>
        /// <returns>New Context object.</returns>
        IContext GetContext();
    }
}