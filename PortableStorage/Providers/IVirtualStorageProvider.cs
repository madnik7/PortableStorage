using System;
using System.IO;
using System.Threading.Tasks;

namespace PortableStorage.Providers
{
    public interface IVirtualStorageProvider
    {
        IStorageProvider CreateStorageProvider(Stream stream, Uri streamUri, string streamName);
    }
}
