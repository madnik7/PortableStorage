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
    }
}
