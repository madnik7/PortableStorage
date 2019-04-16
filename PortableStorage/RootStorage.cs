using PortableStorage.Providers;
using System;
using System.Collections.Generic;
using System.Text;

namespace PortableStorage
{
    public class RootStorage : Storage, IDisposable
    {
        public RootStorage(IStorageProvider provider, StorageOptions options = null)
             : base(provider, options)
        {
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue)
                return;

            if (disposing)
            {
                // dispose managed state (managed objects).
                DisposeInternal();
                disposedValue = true;
            }

            // free unmanaged resources (unmanaged objects) and override a finalizer below.
            // set large fields to null.
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
