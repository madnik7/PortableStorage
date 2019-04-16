using PortableStorage.Providers;
using System.Collections.Generic;

namespace PortableStorage
{
    public class StorageOptions
    {
        public StorageOptions()
        {
            VirtualStorageProviders.Add("zip", new ZipVirualStorageProvider());
        }

        public IDictionary<string, IVirtualStorageProvider> VirtualStorageProviders { get; } = new Dictionary<string, IVirtualStorageProvider>();
        public int CacheTimeout { get; set; } = 1000;
    }

}
