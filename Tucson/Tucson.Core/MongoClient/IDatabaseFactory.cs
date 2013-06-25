using System;

namespace Tucson.MongoClient
{
    /// <summary>
    /// Generic interface for generating new MongoDB Database objects.
    /// </summary>
    public interface IDatabaseFactory : IDisposable
    {
        /// <summary>
        /// Creates a new ICollectionProvider object.
        /// </summary>
        ICollectionProvider GetDatabase();


    }
}