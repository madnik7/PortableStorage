using System;
using System.IO;

namespace PortableStorage.Providers
{
    public class ZipVirualStorageProvider : IVirtualStorageProvider
    {
        public IStorageProvider CreateStorageProvider(Stream stream, Uri streamUri, string streamName)
        {
            return new ZipStorgeProvider(stream, streamUri, streamName);
        }
    }
}
