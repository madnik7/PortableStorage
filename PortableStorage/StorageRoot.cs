using PortableStorage.Providers;
using System;
using System.Collections.Generic;
using System.Text;

namespace PortableStorage
{
    public class StorageRoot : Storage, IDisposable
    {
        public StorageRoot(IStorageProvider provider, StorageOptions options) :
            base(provider, options)
        {
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                Close();
        }

        // free unmanaged resources (unmanaged objects) and override a finalizer below.
        // set large fields to null.

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
