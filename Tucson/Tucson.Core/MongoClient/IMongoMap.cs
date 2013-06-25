using System;

namespace Tucson.MongoClient
{
    /// <summary>
    /// Default interface implying that the class implements a Mongo Map
    /// </summary>
    public interface IMongoMap
    {
        /// <summary>
        /// A function that returns True if the Type is a MappedType
        /// </summary>
        Func<Type, bool> IsMappedType { get; }

        /// <summary>
        /// A method that registers all of the class maps for Mongo
        /// </summary>
        void RegisterClassMaps();
    }
}
