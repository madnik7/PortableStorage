using PortableStorage.Providers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PortableStorage
{
    public class StorageOptions
    {
        public StorageOptions()
        {
            VirtualStorageProviders = new Dictionary<string, IVirtualStorageProvider>(StringComparer.OrdinalIgnoreCase)
            {
                { ".zip", new ZipVirualStorageProvider() }
            };
        }

        public IDictionary<string, IVirtualStorageProvider> VirtualStorageProviders { get; }
        public int CacheTimeout { get; set; } = -1;
        public bool IgnoreCase { get; set; } = true;
        public bool LeaveProviderOpen { get; set; } = false;
    }

}
