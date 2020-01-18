using PortableStorage.Providers;
using System;
using System.Collections.Generic;

namespace PortableStorage
{
    public class StorageOptions
    {
        public StorageOptions()
        {
            VirtualStorageProviders.Add(".zip", new ZipVirualStorageProvider());
        }

        public IDictionary<string, IVirtualStorageProvider> VirtualStorageProviders { get; } = new Dictionary<string, IVirtualStorageProvider>(StringComparer.InvariantCultureIgnoreCase);
        public int CacheTimeout { get; set; } = -1;
        public bool IgnoreCase { get; set; } = true;
        public bool LeaveProviderOpen { get; set; } = false;
    }

}
