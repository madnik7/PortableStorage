using System.IO;

namespace PortableStorage.Providers
{
    public class CreateStreamResult
    {
        public StorageEntryBase EntryBase { get; set; }
        public Stream Stream { get; set; }
    }
}
