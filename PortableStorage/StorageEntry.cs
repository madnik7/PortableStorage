using System;
using System.IO;
using System.Text;

namespace PortableStorage
{
    public class StorageEntry : StorageEntryBase
    {
        internal StorageRoot Root { get;  set; }
        private Storage StreamParent => IsStream ? Parent : throw new InvalidOperationException("Invalid operation for a stream entry!");

        public bool IsVirtualStorage { get; internal set; }
        public Storage Parent { get; internal set; }
        public string Path { get; internal set; }
        public bool IsStream { get; internal set; }
        public bool IsHidden => Attributes.HasFlag(StreamAttribute.Hidden) || (!string.IsNullOrEmpty(Name) && Name[0] == '.');
        public bool Exists => Root != null || Parent.TryGetEntry(Name, out StorageEntry entry) && entry.IsStorage == IsStorage;
        public Storage OpenStorage() => Root ?? Parent.OpenStorage(Name);
        public Stream OpenStreamRead() => StreamParent.OpenStreamRead(Name);
        public Stream OpenStreamWrite(bool truncate) => StreamParent.OpenStreamWrite(Name, truncate);
        public byte[] ReadAllBytes() => StreamParent.ReadAllBytes(Name);
        public string ReadAllText() => StreamParent.ReadAllText(Name);
        public string ReadAllText(Encoding encoding) => StreamParent.ReadAllText(Name, encoding);
        public void WriteAllText(string text) => StreamParent.WriteAllText(Name, text);
        public void WriteAllText(string text, Encoding encoding) => StreamParent.WriteAllText(Name, text, encoding);

        public void Delete()
        {
            if (Root != null)
                throw new InvalidOperationException("Could not delete the Root Storage!");

            if (IsStream)
                Parent.DeleteStream(Name);
            else
                Parent.DeleteStorage(Name);
        }
    }
}
