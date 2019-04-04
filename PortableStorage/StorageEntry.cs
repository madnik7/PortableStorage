namespace PortableStorage
{
    public class StorageEntry : StorageEntryBase
    {
        public Storage Parent { get; set; }
        public string Path { get; set; }
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
