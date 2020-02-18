using System.IO;
using System.Text;

namespace PortableStorage
{
    public class StorageEntry : StorageEntryBase
    {
        public bool IsVirtualStorage { get; internal set; }
        public Storage Parent { get; internal set; }
        public string Path { get; internal set; }
        public bool IsStream { get; internal set; }
        public bool IsHidden => Attributes.HasFlag(StreamAttribute.Hidden) || (!string.IsNullOrEmpty(Name) && Name[0] == '.');
        public bool Exists
        {
            get
            {
                try
                {
                    var entry = Parent.GetEntry(Name);
                    return entry.IsStorage == IsStorage;
                }
                catch (Exceptions.StorageNotEnoughSpaceException)
                {
                    return false;
                }
            }
        }

        public Storage OpenStorage() => Parent.OpenStorage(Name);
        public Stream OpenStreamRead() => Parent.OpenStreamRead(Name);
        public Stream OpenStreamWrite(bool truncate) => Parent.OpenStreamWrite(Name, truncate);
        public byte[] ReadAllBytes() => Parent.ReadAllBytes(Name);
        public string ReadAllText() => Parent.ReadAllText(Name);
        public string ReadAllText(Encoding encoding) => Parent.ReadAllText(Name, encoding);
        public void WriteAllText(string text) => Parent.WriteAllText(Name, text);
        public void WriteAllText(string text, Encoding encoding) => Parent.WriteAllText(Name, text, encoding);

        public void Delete()
        {
            if (IsStream)
                Parent.DeleteStream(Name);
            else
                Parent.DeleteStorage(Name);
        }
    }
}
