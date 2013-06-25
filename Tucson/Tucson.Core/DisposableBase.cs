using System;

namespace Tucson
{
    /// <summary>
    /// Provides a simple base class for all IDisposable classes.  Implements standard Dispose() pattern.
    /// </summary>
    public abstract class DisposableBase : IDisposable
    {
        /// <summary>
        /// Standard GC destructor, calls Dispose(false);
        /// </summary>
        ~DisposableBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes of class instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implement your dispose method using this protected method.
        /// </summary>
        /// <param name="disposing">True if the user is explictly calling Dispose(), False if occurring during garbage collection</param>
        protected abstract void Dispose(bool disposing);
    }
}
