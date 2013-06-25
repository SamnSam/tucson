
using System;
using MongoDB.Driver.Builders;

namespace Tucson.MongoClient
{
    /// <summary>
    /// Thin wrapper for the objects needed to define an index.
    /// </summary>
    [Obsolete("Index Definitions should no longer be used, as indexes are now going to be handled explicitly by DBAs rather than in code.")]
    public class IndexDefinition
    {
        public IndexKeysBuilder IndexKeysBuilder { get; private set; }
        public IndexOptionsBuilder IndexOptionsBuilder { get; private set; }

        public IndexDefinition(IndexKeysBuilder indexKeysBuilder) : this(indexKeysBuilder, null) { }

        public IndexDefinition(IndexKeysBuilder indexKeysBuilder, IndexOptionsBuilder indexOptionsBuilder)
        {
            IndexKeysBuilder = indexKeysBuilder;
            IndexOptionsBuilder = indexOptionsBuilder;
        }
    }
}
