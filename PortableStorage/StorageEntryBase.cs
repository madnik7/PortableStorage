using System;

namespace PortableStorage
{
    public class StorageEntryBase
    {
        public Uri Uri { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTime LastWriteTime { get; set; }
        public StreamAttribute Attributes { get; set; }
        public bool IsStorage { get; set; }
    }
}
