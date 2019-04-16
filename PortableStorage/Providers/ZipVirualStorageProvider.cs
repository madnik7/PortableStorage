using System;
using System.IO;

namespace PortableStorage.Providers
{
    public class ZipVirualStorageProvider : IVirtualStorageProvider
    {
        public IStorageProvider CreateStorage(Stream stream, Uri streamUri, string streamName)
        {
            return new ZipStorgeProvider(stream, streamUri, streamName);
        }
    }
}
