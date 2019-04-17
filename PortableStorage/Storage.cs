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

        public int CacheTimeout => Parent?.CacheTimeout ?? _cacheTimeoutFiled;
        public static readonly char SeparatorChar = '/';
        public Storage Parent { get; }

        private readonly IStorageProvider _provider;
        private readonly int _cacheTimeoutFiled;
        private readonly IDictionary<string, IVirtualStorageProvider> _virtualStorageProviders;
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
            _virtualStorageProviders = options.VirtualStorageProviders;
        }

        private Storage(IStorageProvider provider, Storage parent)
        {
            _provider = provider ?? throw new ArgumentNullException("provider");
            _virtualStorageProviders = parent._virtualStorageProviders;
            Parent = parent ?? throw new ArgumentNullException("parent");
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

        public Stream OpenStream(string path, StreamMode mode, StreamAccess access, StreamShare share, int bufferSize = 0)
        {
            // manage path
            var parentPath = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath))
                return OpenStorage(parentPath).OpenStream(name, mode, access, share, bufferSize);

            //check mode
            if (mode == StreamMode.Append || mode == StreamMode.Truncate)
                if (access == StreamAccess.Write && access != StreamAccess.ReadWrite)
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

        public StorageEntry[] GetStorageEntries(string searchPattern = null)
        {
            return GetEntries(searchPattern).Where(x => x.IsStorage).ToArray();
        }

        public StorageEntry[] GetStreamEntries(string searchPattern = null)
        {
            return GetEntries(searchPattern).Where(x => !x.IsStorage).ToArray();
        }

        public StorageEntry[] GetEntries(string searchPattern = null)
        {
            //check is cache available
            lock (_lockObject)
            {
                if (IsCacheAvailable)
                {
                    if (!string.IsNullOrEmpty(searchPattern))
                    {
                        var regXpattern = WildcardToRegex(searchPattern);
                        return _entryCache.Where(x => Regex.IsMatch(x.Key, regXpattern)).Select(x => x.Value).ToArray();
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
                    var regXpattern = WildcardToRegex(searchPattern);
                    entries = entries.Where(x => Regex.IsMatch(x.Name, regXpattern)).ToArray();
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
            // manage by path
            var parentPath = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath))
                return OpenStorage(parentPath).OpenStorage(System.IO.Path.GetFileName(name));

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
                if (storageEntry.IsVirtualStorage && _virtualStorageProviders.TryGetValue(System.IO.Path.GetExtension(name), out IVirtualStorageProvider virtualStorageProvider))
                {
                    var stream = OpenStreamRead(name);
                    _internalObjects.Add(new WeakReference<IDisposable>(stream)); //should be disposed by RootStorage.Dispose

                    storageProvider = virtualStorageProvider.CreateStorageProvider(stream, storageEntry.Uri, name);
                }
                else
                {
                    storageProvider = _provider.OpenStorage(uri);
                }


                var storage = new Storage(storageProvider, this);
                AddToCache(name, storage);
                return storage;
            }
            catch (StorageNotFoundException)
            {
                ClearCache();
                throw;
            }
        }

        public Storage CreateStorage(string path, bool openIfAlreadyExists = true)
        {
            // manage by path
            var parentPath = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath))
                return CreateStorage(parentPath, true).CreateStorage(System.IO.Path.GetFileName(name), openIfAlreadyExists);

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
            var storage = new Storage(result.Storage, this);

            AddToCache(name, storage);
            AddToCache(entry);
            return storage;
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
            // manage by path
            var parentPath = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath))
            {
                OpenStorage(parentPath).RemoveStream(name);
                return;
            }

            var uri = GetStreamEntry(name).Uri;
            _provider.RemoveStream(uri);

            //update cache
            _entryCache.TryRemove(name, out _);
        }

        public void RemoveStorage(string path)
        {
            // manage by path
            var parentPath = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath))
            {
                OpenStorage(parentPath).RemoveStorage(name);
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
            // manage by path
            var parentPath = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath))
            {
                OpenStorage(parentPath).Rename(name, desName);
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

        public StorageEntry GetStorageEntry(string path)
        {
            return GetEntryHelper(path, true, false);
        }

        public StorageEntry GetStreamEntry(string path)
        {
            return GetEntryHelper(path, false, true);
        }

        public StorageEntry GetEntry(string path)
        {
            return GetEntryHelper(path, true, true);
        }

        private StorageEntry GetEntryHelper(string path, bool includeStorage, bool includeStream)
        {
            // manage by path
            var parentPath = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath))
                return OpenStorage(parentPath).GetEntryHelper(System.IO.Path.GetFileName(name), includeStorage, includeStream);

            // manage by name
            var entries = GetEntries(name);
            var item = entries.Where(x => x.Name == name).FirstOrDefault();
            if (item != null && includeStorage && item.IsStorage) return item;
            if (item != null && includeStream && item.IsStream) return item;
            throw new StorageNotFoundException(Uri, name);
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

        /// <summary>
        /// A Stream opened in the specified mode and path, with read/write access and not shared.
        /// </summary>
        public Stream OpenStream(string path, StreamMode mode, int bufferSize = 0)
        {
            return OpenStream(path, mode, StreamAccess.ReadWrite, StreamShare.None, bufferSize);
        }

        public Stream OpenStreamRead(string path, int bufferSize = 0)
        {
            return OpenStream(path, StreamMode.Open, StreamAccess.Read, StreamShare.Read, bufferSize);
        }

        public Stream OpenStreamWrite(string path)
        {
            return OpenStreamWrite(path, StreamShare.None);
        }

        public Stream OpenStreamWrite(string path, StreamShare share)
        {
            try
            {
                return OpenStream(path, StreamMode.Open, StreamAccess.Write, share);
            }
            catch (StorageNotFoundException)
            {
                return CreateStream(path, share);
            }

        }

        public Stream CreateStream(string name, bool overwriteExisting = false, int bufferSize = 0)
        {
            return CreateStream(name, StreamShare.None, overwriteExisting, bufferSize);
        }

        public Stream CreateStream(string path, StreamShare share, bool overwriteExisting = false, int bufferSize = 0)
        {
            // manage by path
            var parentPath = System.IO.Path.GetDirectoryName(path);
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath))
                return CreateStorage(parentPath, true).CreateStream(name, share, overwriteExisting, bufferSize);

            if (EntryExists(name) && !overwriteExisting)
                throw new IOException("Entry already exists!");

            //try to delete the old one
            if (overwriteExisting)
                RemoveStream(name);

            //create new stream
            var result = _provider.CreateStream(name, StreamAccess.ReadWrite, share, bufferSize);
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
            using (var stream = OpenStreamWrite(path))
            using (var sr = new StreamWriter(stream, encoding))
                sr.Write(text);
        }

        public void WriteAllText(string path, string text)
        {
            using (var stream = OpenStreamWrite(path))
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
            if (_virtualStorageProviders.TryGetValue(System.IO.Path.GetExtension(storageProviderEntry.Name), out _))
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
