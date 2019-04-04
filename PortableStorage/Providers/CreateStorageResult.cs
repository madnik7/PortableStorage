namespace PortableStorage.Providers
{
    public class CreateStorageResult
    {
        public StorageEntryBase Entry { get; set; }
        public IStorageProvider Storage { get; set; }
    }
}
