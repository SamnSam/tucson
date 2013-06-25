using System;

namespace Tucson.MongoClient
{
    public interface IUpdateableEntity
    {
        /// <summary>
        /// Gets
        /// </summary>
        DateTime? CreateDate { get; set; }


        DateTime? UpdateDate { get; set; }
    }
}