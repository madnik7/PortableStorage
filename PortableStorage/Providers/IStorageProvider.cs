using System;
using System.IO;
using System.Threading.Tasks;

namespace PortableStorage.Providers
{
    public interface IStorageProvider : IDisposable
    {
        Uri Uri { get; }
        string Name { get; }
        bool IsGetEntriesBySearchPatternFast { get; }
        bool IsGetEntryUriByNameFast { get; }
        long GetFreeSpace();
        CreateStorageResult CreateStorage(string name);
        CreateStreamResult CreateStream(string name, StreamAccess access, StreamShare share, int bufferSize);
        IStorageProvider OpenStorage(Uri uri);
        Stream OpenStream(Uri uri, StreamMode mode, StreamAccess access, StreamShare share, int bufferSize);
        void RemoveStream(Uri uri);
        void RemoveStorage(Uri uri);
        Uri Rename(Uri uri, string desName);
        void SetAttributes(Uri uri, StreamAttributes attributes);
        StorageEntryBase[] GetEntries(string searchPattern = null);
    }
}
