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

        public static StorageRoot CreateStorage(string zipPath, StorageOptions storageOptions = null)
        {
            var provider = new ZipStorgeProvider(zipPath);
            var ret = new StorageRoot(provider, storageOptions);
            return ret;
        }

        public static StorageRoot CreateStorage(Stream stream, Uri streamUri = null, string streamName = null, StorageOptions storageOptions = null)
        {
            var provider = new ZipStorgeProvider(stream, streamUri, streamName);
            var ret = new StorageRoot(provider, storageOptions);
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

        public ZipStorgeProvider(Stream stream, Uri streamUri = null, string streamName = null, bool leaveStreamOpen = false)
        {
            //var syncStream = new SyncStream(stream);
            _zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, leaveStreamOpen);
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
        public void SetAttributes(Uri uri, StreamAttributes attributes) => throw new NotSupportedException();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
        public StreamAttributes GetAttributes(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            StreamAttributes attr = 0;
            return attr;
        }

        public IStorageProvider OpenStorage(Uri uri)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            lock (_zipArchive)
            {
                var path = PathFromUri(uri);
                var exists = _zipArchive.Entries.Any(x => GetEntryFolderName(x.FullName).IndexOf(path, StringComparison.OrdinalIgnoreCase) == 0);
                if (!exists)
                    throw new StorageNotFoundException(uri);

                var ret = new ZipStorgeProvider(this, path);
                return ret;
            }
        }

        private static string GetEntryFolderName(string fullName)
        {
            return GetEntryFolderName(fullName, out _);
        }

        private static string GetEntryFolderName(string fullName, out string fixFullName)
        {
            //fix and add first separator to  fullname
            fullName = fullName.Replace('\\', Storage.SeparatorChar);
            fullName = Storage.SeparatorChar + fullName.TrimStart(Storage.SeparatorChar);
            fixFullName = fullName;

            // find folder
            var dirName = Path.GetDirectoryName(fullName).Replace('\\', Storage.SeparatorChar); //change backslash to slash
            return PathUtil.AddLastSeparator(dirName);
        }

        public StorageEntryBase[] GetEntries(string searchPattern)
        {
            lock (_zipArchive)
            {

                var ret = new List<StorageEntryBase>();
                var folders = new Dictionary<string, ZipArchiveEntry>();
                foreach (var entry in _zipArchive.Entries)
                {
                    var entryFolder = GetEntryFolderName(entry.FullName, out string fullName);
                    if (entryFolder.IndexOf(_path, StringComparison.OrdinalIgnoreCase) != 0)
                        continue; //not exists in current folder

                    // find item part
                    // if current folder is "/folder1/sub1/aa.txt" then itemPart is "sub1/aa.txt"
                    var itemPart = fullName[_path.Length..].Replace('\\', Storage.SeparatorChar);
                    if (string.IsNullOrEmpty(itemPart))
                        continue; //no item part means it posint to current storage

                    // add file in current path
                    if (entryFolder == _path && !string.IsNullOrEmpty(entry.Name)) // if entry.Name is empty it means it is empty folder not a file
                    {
                        ret.Add(StorageProviderEntryFromZipEntry(entry));
                    }
                    // add folder in current path 
                    else
                    {
                        var nextSeparatorIndex = itemPart.IndexOf(Storage.SeparatorChar, StringComparison.OrdinalIgnoreCase);
                        if (nextSeparatorIndex == -1) nextSeparatorIndex = itemPart.Length;
                        var folderName = itemPart.Substring(0, nextSeparatorIndex);
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
                        Uri = PathToUri(PathUtil.AddLastSeparator(Path.Combine(_path, folder.Key)))
                    });
                }

                return ret.ToArray();
            }
        }

        public Stream OpenStream(Uri uri, StreamMode mode, StreamAccess access, StreamShare share, int bufferSize)
        {
            if (uri is null) throw new ArgumentNullException(nameof(uri));

            if (mode != StreamMode.Open)
                throw new NotSupportedException($"ZipStorgeProvider does not support mode: {mode}");

            lock (_zipArchive)
            {
                var path = PathFromUri(uri);
                var stream = _zipArchive.GetEntry(path).Open();
                return stream;
            }
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
            StreamAttributes attr = 0;
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

        private bool _disposedValue = false; // To detect redundant calls
        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
                return;

            if (disposing)
            {
                // dispose managed state (managed objects).
                _zipArchive.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override a finalizer below.
            // set large fields to null.

            _disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
