using System.IO;

namespace PortableStorage.Providers
{
    public class CreateStreamResult
    {
        public StorageEntryBase Entry { get; set; }
        public Stream Stream { get; set; }
    }
}
