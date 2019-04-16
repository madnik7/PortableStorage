using PortableStorage.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web;

namespace PortableStorage.Providers
{
    public class ZipStorgeProvider : IStorageProvider
    {
        public Uri Uri => PathToUri(_path);
        public bool IsGetEntriesBySearchPatternFast => false;
        public bool IsGetEntryUriByNameFast => true;

        public static Storage CreateStorage(string zipPath, StorageOptions storageOptions = null)
        {
            var provider = new ZipStorgeProvider(zipPath);
            var ret = new Storage(provider, storageOptions);
            return ret;
        }

        public static Storage CreateStorage(Stream stream, Uri streamUri = null, string streamName = null, StorageOptions storageOptions = null)
        {
            var provider = new ZipStorgeProvider(stream, streamUri, streamName);
            var ret = new Storage(provider, storageOptions);
            return ret;
        }

        private readonly string _path = "/";
        private readonly ZipArchive _zipArchive;
        private readonly string _name;
        private readonly Uri _streamUri;

        public ZipStorgeProvider(string zipPath)
            : this(File.OpenRead(zipPath), new Uri(zipPath), Path.GetFileName(zipPath))
        {

        }

        public ZipStorgeProvider(Stream stream, Uri streamUri = null, string streamName = null)
        {
            var syncStream = new SyncStream(stream);
            _zipArchive = new ZipArchive(syncStream);
            _streamUri = streamUri;
            _name = streamName;
        }

        private ZipStorgeProvider(ZipStorgeProvider parent, string path)
        {
            _path = path;
            _zipArchive = parent._zipArchive;
        }

        public string Name => _name ?? Path.GetFileName(PathUtil.RemoveLastSeparator(_path));

        public long GetFreeSpace()
        {
            return 0;
        }

        public CreateStorageResult CreateStorage(string name) => throw new NotSupportedException();
        public CreateStreamResult CreateStream(string name, StreamAccess access, StreamShare share, int bufferSize) => throw new NotSupportedException();
        public Uri Rename(Uri uri, string desName) => throw new NotSupportedException();
        public void RemoveStream(Uri uri) => throw new NotSupportedException();
        public void RemoveStorage(Uri uri) => throw new NotSupportedException();
        public void SetAttributes(Uri uri, StreamAttribute attributes) => throw new NotSupportedException();

        public StreamAttribute GetAttributes(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            StreamAttribute attr = 0;
            return attr;
        }

        public IStorageProvider OpenStorage(Uri uri)
        {
            var path = PathFromUri(uri);
            var exists = _zipArchive.Entries.Any(x => GetEntryFolderName(x.FullName).IndexOf(path) == 0);
            if (!exists)
                throw new StorageNotFoundException(uri);

            var ret = new ZipStorgeProvider(this, path);
            return ret;
        }

        private string GetEntryFolderName(string fullName)
        {
            var dirName = Path.GetDirectoryName(fullName)
                .Replace('\\', Storage.SeparatorChar) //change backslash to slash
                .Trim(Storage.SeparatorChar); //remove start and end separator
            dirName = Storage.SeparatorChar + dirName;
            return dirName == Storage.SeparatorChar.ToString() ? dirName : PathUtil.AddLastSeparator(dirName);
        }

        public StorageEntryBase[] GetEntries(string searchPattern)
        {
            var ret = new List<StorageEntryBase>();
            var folders = new Dictionary<string, ZipArchiveEntry>();
            foreach (var entry in _zipArchive.Entries)
            {
                var entryFolder = GetEntryFolderName(entry.FullName);

                // add file in current path
                if (entryFolder == _path)
                {
                    ret.Add(StorageProviderEntryFromZipEntry(entry));
                }
                // add folder in current path (/aa 
                else if (entryFolder.IndexOf(_path) == 0)
                {
                    var folderName = entryFolder.Substring(_path.Length, entryFolder.IndexOf(Storage.SeparatorChar, _path.Length) - _path.Length);
                    if (!folders.TryGetValue(folderName, out ZipArchiveEntry lastEntry) || lastEntry.LastWriteTime < entry.LastWriteTime)
                        folders[folderName] = entry;
                }
            }

            // add folders
            foreach (var folder in folders)
            {
                ret.Add(new StorageEntryBase()
                {
                    Attributes = 0,
                    IsStorage = true,
                    LastWriteTime = folder.Value.LastWriteTime.DateTime,
                    Size = 0,
                    Name = folder.Key,
                    Uri = PathToUri(GetEntryFolderName(folder.Value.FullName))
                });
            }

            return ret.ToArray();
        }

        public Stream OpenStream(Uri uri, StreamMode mode, StreamAccess access, StreamShare share, int bufferSize)
        {
            if (mode != StreamMode.Open)
                throw new NotSupportedException($"ZipStorgeProvider does not support mode: {mode}");

            var path = PathFromUri(uri);
            return _zipArchive.GetEntry(path).Open();
        }


        private string PathFromUri(Uri uri)
        {
            var query = HttpUtility.ParseQueryString(uri.Query);
            return query["fullname"];
        }

        private Uri PathToUri(string path)
        {
            var uri = new UriBuilder(_streamUri?.ToString() ?? $"zip://zipstorgeprovider")
            {
                Query = $"fullname={path}" // we can't use uri path because some zip save "/" as back slash
            };
            return uri.Uri;
        }

        private StorageEntryBase StorageProviderEntryFromZipEntry(ZipArchiveEntry entry)
        {
            StreamAttribute attr = 0;
            var ret = new StorageEntryBase()
            {
                Uri = PathToUri(entry.FullName),
                Attributes = attr,
                IsStorage = false,
                Name = entry.Name,
                LastWriteTime = entry.LastWriteTime.DateTime,
                Size = entry.Length
            };

            return ret;
        }
    }
}
