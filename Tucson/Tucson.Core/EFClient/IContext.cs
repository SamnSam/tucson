using System.Data;

namespace Tucson.EFClient
{
    /// <summary>
    /// Provides functionality to query from a database and group together 
    /// changes that will then be written back to the store
    /// </summary>
    public interface IContext : ISetProvider
    {
        /// <summary>
        /// Saves all changes made in this context to the underlying database.
        /// </summary>
        void Commit();
    }
}
