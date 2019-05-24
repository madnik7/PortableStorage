using PortableStorage.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PortableStorage.Providers;

namespace PortableStorage
{
    public class Storage : IDisposable
    {
        private class StorageCache
        {
            public readonly DateTime cacheTime = DateTime.Now;
            public Storage storage;
        }

        public static readonly char SeparatorChar = '/';
        public int CacheTimeout => Parent?.CacheTimeout ?? _cacheTimeoutFiled;
        public Storage Parent { get; }
        public IDictionary<string, IVirtualStorageProvider> VirtualStorageProviders { get; }
        public bool IgnoreCase { get; }

        private readonly IStorageProvider _provider;
        private readonly int _cacheTimeoutFiled;
        private DateTime _lastCacheTime = DateTime.MinValue;
        private readonly ConcurrentDictionary<string, StorageCache> _storageCache = new ConcurrentDictionary<string, StorageCache>();
        private readonly ConcurrentDictionary<string, StorageEntry> _entryCache = new ConcurrentDictionary<string, StorageEntry>();
        private readonly object _lockObject = new object();
        private string _name;
        private readonly List<WeakReference<IDisposable>> _internalObjects = new List<WeakReference<IDisposable>>();

        public Storage(IStorageProvider provider, StorageOptions options)
        {
            options = options ?? new StorageOptions();
            _provider = provider ?? throw new ArgumentNullException("provider");
            _cacheTimeoutFiled = options.CacheTimeout == -1 ? 1000 : options.CacheTimeout;
            VirtualStorageProviders = options.VirtualStorageProviders;
            IgnoreCase = options.IgnoreCase;
        }

        private Storage(IStorageProvider provider, Storage parent)
        {
            _provider = provider ?? throw new ArgumentNullException("provider");
            Parent = parent ?? throw new ArgumentNullException("parent");
            IgnoreCase = parent.IgnoreCase;
            VirtualStorageProviders = parent.VirtualStorageProviders;
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls
        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
                return;

            if (disposing)
            {
                // dispose managed state (managed objects).
                foreach (var item in _internalObjects)
                    if (item.TryGetTarget(out IDisposable target))
                        target?.Dispose();
                _provider.Dispose();
                _disposedValue = true;
            }

            // free unmanaged resources (unmanaged objects) and override a finalizer below.
            // set large fields to null.
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

        public string Path => (Parent == null) ? SeparatorChar.ToString() : PathCombine(Parent.Path, Name);
        public bool IsRoot => Parent == null;
        public Storage RootStorage => Parent ?? this;

        public string Name
        {
            get
            {
                if (_name == null)
                    _name = _provider.Name; // cache the name, it might be so slow
                return _name;
            }
        }

        public Uri Uri => _provider.Uri;

        private bool IsCacheAvailable
        {
            get
            {
                lock (_lockObject)
                    return CacheTimeout != 0 && _lastCacheTime.AddSeconds(CacheTimeout) > DateTime.Now;
            }
        }

        private Storage GetStorageForPath(string path, out string name, bool createIfNotExists = false)
        {
            // fix backslash
            path = path.Replace('\\', SeparatorChar);

            // manage path from root
            if (path.Length > 0 && path[0] == SeparatorChar)
                throw new ArgumentException("Path can not start with slash!", nameof(path));

            var parentPath = System.IO.Path.GetDirectoryName(path);
            name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath))
                return createIfNotExists ? CreateStorage(parentPath) : OpenStorage(parentPath);

            return null;
        }

        public Stream OpenStream(string path, StreamMode mode, StreamAccess access, StreamShare share, int bufferSize = 0)
        {
            // manage path
            var storage = GetStorageForPath(path, out string name);
            if (storage != null)
                return storage.OpenStream(name, mode, access, share, bufferSize);

            //check mode
            if (mode == StreamMode.Append || mode == StreamMode.Truncate)
                if (access != StreamAccess.Write && access != StreamAccess.ReadWrite)
                    throw new ArgumentException($"{mode} needs StreamAccess.write access.");

            var entry = GetStreamEntry(name);
            try
            {
                var ret = _provider.OpenStream(entry.Uri, mode, access, share, bufferSize);
                return ret;
            }
            catch (StorageNotFoundException)
            {
                ClearCache();
                throw;
            }
        }

        public long GetFreeSpace()
        {
            return _provider.GetFreeSpace();
        }

        /// <param name="searchPattern">can include path and wildcard. eg: /folder/file.*</param>
        public StorageEntry[] GetStorageEntries(string searchPattern = null)
        {
            return GetEntries(searchPattern).Where(x => x.IsStorage).ToArray();
        }

        /// <param name="searchPattern">can include path and wildcard. eg: /folder/file.*</param>
        public StorageEntry[] GetStreamEntries(string searchPattern = null)
        {
            return GetEntries(searchPattern).Where(x => !x.IsStorage).ToArray();
        }

        /// <param name="searchPattern">can include path and wildcard. eg: /folder/file.*</param>
        public StorageEntry[] GetEntries(string searchPattern = null)
        {
            // manage path
            if (!string.IsNullOrEmpty(searchPattern))
            {
                var storage = GetStorageForPath(searchPattern, out string newSearchPattern);
                if (storage != null)
                    return storage.GetEntries(newSearchPattern);
                searchPattern = newSearchPattern;
            }


            //check is cache available
            lock (_lockObject)
            {
                if (IsCacheAvailable)
                {
                    if (!string.IsNullOrEmpty(searchPattern))
                    {
                        var regexOptions = IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                        var regXpattern = WildcardToRegex(searchPattern);
                        return _entryCache.Where(x => Regex.IsMatch(x.Key, regXpattern, regexOptions)).Select(x => x.Value).ToArray();
                    }
                    return _entryCache.Select(x => x.Value).ToArray();
                }
            }

            //use provider
            var pattern = _provider.IsGetEntriesBySearchPatternFast ? searchPattern : null;
            pattern = null;
            var providerEntires = _provider.GetEntries(pattern);
            var entries = StorageEntryFromStorageEntryProvider(providerEntires);

            //update cache
            _entryCache.Clear();
            foreach (var entry in entries)
                AddToCache(entry);

            //cache the result when all item is returned
            if (pattern == null)
            {
                lock (_lockObject)
                {
                    _lastCacheTime = DateTime.Now;
                }

                //apply searchPattern
                if (searchPattern != null)
                {
                    var regexOptions = IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                    var regXpattern = WildcardToRegex(searchPattern);
                    entries = entries.Where(x => Regex.IsMatch(x.Name, regXpattern, regexOptions)).ToArray();
                }
            }

            return entries;
        }

        public void ClearCache()
        {
            lock (_lockObject)
            {
                _storageCache.Clear();
                _entryCache.Clear();
                _lastCacheTime = DateTime.MinValue;
            }
        }

        public Storage OpenStorage(string path)
        {
            // manage path
            var storage = GetStorageForPath(path, out string name);
            if (storage != null)
                return storage.OpenStorage(name);

            //use storage cache
            if (_storageCache.TryGetValue(name, out StorageCache storageCache))
            {
                if (storageCache.cacheTime.AddSeconds(CacheTimeout) > DateTime.Now)
                    return storageCache.storage;
                _storageCache.TryRemove(name, out _);
            }

            // open storage and add it to cache
            var storageEntry = GetStorageEntry(name);
            var uri = storageEntry.Uri;
            try
            {
                IStorageProvider storageProvider;
                if (storageEntry.IsVirtualStorage && VirtualStorageProviders.TryGetValue(System.IO.Path.GetExtension(name), out IVirtualStorageProvider virtualStorageProvider))
                {
                    var stream = OpenStreamRead(name);
                    _internalObjects.Add(new WeakReference<IDisposable>(stream));

                    storageProvider = virtualStorageProvider.CreateStorageProvider(stream, storageEntry.Uri, name);
                }
                else
                {
                    storageProvider = _provider.OpenStorage(uri);
                }


                var newStorage = new Storage(storageProvider, this);
                AddToCache(name, newStorage);
                return newStorage;
            }
            catch (StorageNotFoundException)
            {
                ClearCache();
                throw;
            }
        }

        public Storage CreateStorage(string path, bool openIfAlreadyExists = true)
        {
            // manage path
            var storage = GetStorageForPath(path, out string name, true);
            if (storage != null)
                return storage.CreateStorage(name, openIfAlreadyExists);

            // Check existance, some provider may duplicate the entry with same name
            if (EntryExists(name))
            {
                if (openIfAlreadyExists)
                    return OpenStorage(name);
                else
                    throw new IOException("Entry already exists!");
            }

            var result = _provider.CreateStorage(name);
            var entry = ProviderEntryToEntry(result.Entry);
            var newStorage = new Storage(result.Storage, this);

            AddToCache(name, newStorage);
            AddToCache(entry);
            return newStorage;
        }

        private void AddToCache(string name, Storage storage = null)
        {
            lock (_lockObject)
            {
                _storageCache.TryAdd(name, new StorageCache() { storage = storage });
                _internalObjects.Add(new WeakReference<IDisposable>(storage));
            }
        }

        private void AddToCache(StorageEntry entry)
        {
            lock (_lockObject)
            {
                _entryCache.TryAdd(entry.Name, entry);
            }
        }


        public void RemoveStream(string path)
        {
            // manage path
            var storage = GetStorageForPath(path, out string name);
            if (storage != null)
            {
                storage.RemoveStream(name);
                return;
            }

            var uri = GetStreamEntry(name).Uri;
            _provider.RemoveStream(uri);

            //update cache
            _entryCache.TryRemove(name, out _);
        }

        public void RemoveStorage(string path)
        {
            // manage path
            var storage = GetStorageForPath(path, out string name);
            if (storage != null)
            {
                storage.RemoveStorage(name);
                return;
            }

            var uri = GetStorageEntry(name).Uri;
            _provider.RemoveStorage(uri);

            //update cache
            _storageCache.TryRemove(name, out _);
            _entryCache.TryRemove(name, out _);
        }

        public void Rename(string path, string desName)
        {
            // manage path
            var storage = GetStorageForPath(path, out string name);
            if (storage != null)
            {
                storage.Rename(name, desName);
                return;
            }

            var entry = GetEntry(name);
            var newUri = _provider.Rename(entry.Uri, desName);

            //update the cache
            lock (_lockObject)
            {
                if (_storageCache.TryRemove(name, out StorageCache storageCache))
                {
                    _storageCache.Clear(); //As storage.uri will be change the descendant may change too which can not be cached
                }

                if (_entryCache.TryRemove(name, out StorageEntry storageEntry))
                {
                    entry.Name = desName;
                    entry.Uri = newUri;
                    AddToCache(entry);
                }
            }
        }

        public bool EntryExists(string path)
        {
            try
            {
                return GetEntry(path) != null;
            }
            catch (StorageNotFoundException)
            {
                return false;
            }
        }

        public bool StorageExists(string path)
        {
            try
            {
                return GetStorageEntry(path) != null;
            }
            catch (StorageNotFoundException)
            {
                return false;
            }
        }

        public bool StreamExists(string path)
        {
            try
            {
                return GetStreamEntry(path) != null;
            }
            catch (StorageNotFoundException)
            {
                return false;
            }
        }


        public StorageEntry GetStorageEntry(string path) => GetEntryHelper(path, true, false);
        public bool TryGetStorageEntry(string path, out StorageEntry storageEntry) => TryGetEntryHelper(path, true, false, out storageEntry);

        public StorageEntry GetStreamEntry(string path) => GetEntryHelper(path, false, true);
        public bool TryGetStreamEntry(string path, out StorageEntry storageEntry) => TryGetEntryHelper(path, false, true, out storageEntry);

        public StorageEntry GetEntry(string path) => GetEntryHelper(path, true, true);
        public bool TryGetEntry(string path, out StorageEntry storageEntry) => TryGetEntryHelper(path, true, true, out storageEntry);

        private bool TryGetEntryHelper(string path, bool includeStorage, bool includeStream, out StorageEntry storageEntry)
        {
            try
            {
                storageEntry = GetEntryHelper(path, includeStorage, includeStream);
                return true;
            }
            catch (StorageNotFoundException)
            {
                storageEntry = null;
                return false;
            }
        }

        private StorageEntry GetEntryHelper(string path, bool includeStorage, bool includeStream)
        {
            // manage by name
            var entries = GetEntries(path);
            var item = entries.FirstOrDefault();
            if (item != null && includeStorage && item.IsStorage) return item;
            if (item != null && includeStream && item.IsStream) return item;
            throw new StorageNotFoundException(Uri, path);
        }

        public void SetAttributes(string path, StreamAttribute attributes)
        {
            var entry = GetEntry(path);
            try
            {
                _provider.SetAttributes(entry.Uri, attributes);
                entry.Attributes = attributes;
            }
            catch (NotSupportedException)
            {
            }
        }

        public StreamAttribute GetAttributes(string path)
        {
            var entry = GetEntry(path);
            var ret = entry.Attributes;
            return ret;
        }

        public Stream OpenStreamRead(string path, int bufferSize = 0)
        {
            return OpenStream(path, StreamMode.Open, StreamAccess.Read, StreamShare.Read, bufferSize);
        }

        public Stream OpenStreamWrite(string path, bool truncateIfExists = false)
        {
            try
            {
                return OpenStream(path, truncateIfExists ? StreamMode.Truncate : StreamMode.Open, StreamAccess.Write, StreamShare.None);
            }
            catch (StorageNotFoundException)
            {
                return CreateStream(path, truncateIfExists);
            }
        }

        public Stream CreateStream(string name, bool overwriteExisting = false, int bufferSize = 0)
        {
            return CreateStream(name, StreamShare.None, overwriteExisting, bufferSize);
        }

        public Stream CreateStream(string path, StreamShare share, bool overwriteExisting = false, int bufferSize = 0)
        {
            // manage path
            var storage = GetStorageForPath(path, out string name, true);
            if (storage != null)
                return storage.CreateStream(name, share, overwriteExisting, bufferSize);

            // Manage already exists
            if (EntryExists(name))
            {
                if (overwriteExisting)
                    RemoveStream(name); //try to delete the old one
                else
                    throw new IOException("Entry already exists!");
            }

            //create new stream
            var result = _provider.CreateStream(name, StreamAccess.Write, share, bufferSize);
            var entry = ProviderEntryToEntry(result.EntryBase);
            AddToCache(entry);
            return result.Stream;
        }

        public Storage OpenStorage(string path, bool createIfNotExists)
        {
            try
            {
                return OpenStorage(path);
            }
            catch (StorageNotFoundException)
            {
                if (!createIfNotExists)
                    throw;

                return CreateStorage(path);
            }
        }

        public string ReadAllText(string path)
        {
            using (var stream = OpenStreamRead(path))
            using (var sr = new StreamReader(stream))
            {
                return sr.ReadToEnd();
            }
        }

        public string ReadAllText(string path, Encoding encoding)
        {
            using (var stream = OpenStreamRead(path))
            using (var sr = new StreamReader(stream, encoding))
            {
                return sr.ReadToEnd();
            }
        }

        public void WriteAllText(string path, string text, Encoding encoding)
        {
            using (var stream = OpenStreamWrite(path, true))
            using (var sr = new StreamWriter(stream, encoding))
                sr.Write(text);
        }

        public void WriteAllText(string path, string text)
        {
            using (var stream = OpenStreamWrite(path, true))
            using (var sr = new StreamWriter(stream))
                sr.Write(text);
        }

        public byte[] ReadAllBytes(string path)
        {
            using (var stream = OpenStreamRead(path))
            using (var sr = new BinaryReader(stream))
                return sr.ReadBytes((int)stream.Length);
        }

        public void WriteAllBytes(string path, byte[] bytes)
        {
            using (var stream = OpenStreamWrite(path))
                stream.Write(bytes, 0, bytes.Length);
        }


        public static string PathCombine(string path1, string path2)
        {
            return System.IO.Path.Combine(path1, path2).Replace('\\', SeparatorChar);
        }

        public long GetSize()
        {
            var entries = GetEntries();
            var ret = entries.Sum(x => x.IsStorage ? OpenStorage(x.Name).GetSize() : x.Size);
            return ret;
        }

        public void Copy(string sourcePath, string destinationPath, bool overwrite = false) => Copy(GetEntry(sourcePath), this, destinationPath, overwrite);
        public void Copy(string sourcePath, Storage destinationStorage, string destinationPath, bool overwrite = false) => Copy(GetEntry(sourcePath), destinationStorage, destinationPath, overwrite);
        public void CopyTo(Storage destinationStorage, string destinationPath, bool overwrite = false) => CopyTo(destinationStorage.CreateStorage(destinationPath), overwrite);
        public void CopyTo(Storage destinationStorage, bool overwrite=false)
        {
            foreach (var item in GetEntries())
                Copy(item.Name, destinationStorage, "", overwrite);
        }

        public static void Copy(StorageEntry srcEntry, Storage destinationStorage, string destinationPath, bool overwrite = false)
        {
            // add source filename to destination path if dest path is a folder (ended with separator)
            if (string.IsNullOrEmpty(System.IO.Path.GetFileName(destinationPath)))
                destinationPath = PathCombine(destinationPath, System.IO.Path.GetFileName(srcEntry.Name));

            if (srcEntry.IsStream)
            {
                using (var srcStream = srcEntry.Parent.OpenStreamRead(srcEntry.Name))
                using (var desStream = destinationStorage.CreateStream(destinationPath, overwrite))
                    srcStream.CopyTo(desStream);
            }
            else
            {
                var storage = srcEntry.Parent.OpenStorage(srcEntry.Name);
                storage.CopyTo(destinationStorage, destinationPath, overwrite);
            }
        }



        public static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                              .Replace(@"\*", ".*")
                              .Replace(@"\?", ".")
                       + "$";
        }

        private StorageEntry[] StorageEntryFromStorageEntryProvider(StorageEntryBase[] storageProviderEntry)
        {
            var entries = new List<StorageEntry>(storageProviderEntry.Length);
            foreach (var entryProvider in storageProviderEntry)
                entries.Add(ProviderEntryToEntry(entryProvider));
            return entries.ToArray();
        }

        private StorageEntry ProviderEntryToEntry(StorageEntryBase storageProviderEntry)
        {
            var isVirtualStorage = false;
            if (VirtualStorageProviders.TryGetValue(System.IO.Path.GetExtension(storageProviderEntry.Name), out _))
                isVirtualStorage = true;

            var entry = new StorageEntry()
            {
                Attributes = storageProviderEntry.Attributes,
                IsVirtualStorage = isVirtualStorage,
                IsStorage = storageProviderEntry.IsStorage || isVirtualStorage,
                IsStream = !storageProviderEntry.IsStorage,
                LastWriteTime = storageProviderEntry.LastWriteTime,
                Name = storageProviderEntry.Name,
                Size = storageProviderEntry.Size,
                Uri = storageProviderEntry.Uri,
                Parent = this,
                Path = PathCombine(Path, storageProviderEntry.Name)
            };
            return entry;
        }
    }
}
