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
    public class Storage
    {
        private class StorageCache
        {
            public readonly DateTime cacheTime = DateTime.Now;
            public Storage storage;
        }

        public static readonly char SeparatorChar = '/';

        private readonly IStorageProvider _provider;
        private readonly int _cacheTimeoutFiled;
        private int _cacheTimeout => _parent?._cacheTimeout ?? _cacheTimeoutFiled;
        private DateTime _lastCacheTime = DateTime.MinValue;
        private readonly ConcurrentDictionary<string, StorageCache> _storageCache = new ConcurrentDictionary<string, StorageCache>();
        private readonly ConcurrentDictionary<string, StorageEntry> _entryCache = new ConcurrentDictionary<string, StorageEntry>();
        private readonly object _lockObject = new object();
        private string _name;


        public Storage(IStorageProvider provider, int cacheTimeout = -1)
        {
            _provider = provider ?? throw new ArgumentNullException("provider");
            _cacheTimeoutFiled = cacheTimeout==-1 ? 1000 : cacheTimeout;
        }

        private Storage(IStorageProvider provider, Storage parent)
        {
            _provider = provider ?? throw new ArgumentNullException("provider");
            _parent = parent ?? throw new ArgumentNullException("parent");
        }

        public Storage _parent { get; }

        public string Path => (_parent == null) ? "/" : PathCombine(_parent.Path, Name);

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
                    return _cacheTimeout != 0 && _lastCacheTime.AddSeconds(_cacheTimeout) > DateTime.Now;
            }
        }

        public Stream OpenStream(string name, StreamMode mode, StreamAccess access, StreamShare share, int bufferSize = 0)
        {
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
                _entryCache.TryAdd(entry.Name, entry);

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

        public Storage OpenStorage(string name)
        {
            //use storage cache
            if (_storageCache.TryGetValue(name, out StorageCache storageCache))
            {
                if (storageCache.cacheTime.AddSeconds(_cacheTimeout) > DateTime.Now)
                    return storageCache.storage;
                _storageCache.TryRemove(name, out _);
            }

            // open storage and add it to cache
            var uri = GetStorageEntry(name).Uri;
            try
            {
                var providerStorage = _provider.OpenStorage(uri);
                var storage = new Storage(providerStorage, this);
                _storageCache.TryAdd(name, new StorageCache() { storage = storage });
                return storage;
            }
            catch (StorageNotFoundException)
            {
                ClearCache();
                throw;
            }
        }

        public Storage CreateStorage(string name, bool openIfAlreadyExists = true)
        {
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
            _storageCache.TryAdd(name, new StorageCache() { storage = storage });
            _entryCache.TryAdd(name, entry);
            return storage;
        }

        public void RemoveStream(string name)
        {
            var uri = (GetStreamEntry(name)).Uri;
            _provider.RemoveStream(uri);

            //update cache
            _entryCache.TryRemove(name, out _);
        }

        public void RemoveStorage(string name)
        {
            var uri = GetStorageEntry(name).Uri;
            _provider.RemoveStorage(uri);

            //update cache
            _storageCache.TryRemove(name, out _);
            _entryCache.TryRemove(name, out _);
        }

        public void Rename(string name, string desName)
        {
            var entry = GetEntry(name);
            var newUri = _provider.Rename(entry.Uri, desName);

            //update the cache
            lock (_lockObject)
            {
                if (_storageCache.TryRemove(name, out StorageCache storageCache))
                {
                    _storageCache.Clear(); //storage.uri will be change and descendant may change and can not be cached
                }

                if (_entryCache.TryRemove(name, out StorageEntry storageEntry))
                {
                    entry.Name = desName;
                    entry.Uri = newUri;
                    _entryCache.TryAdd(desName, entry);
                }
            }
        }

        public bool EntryExists(string name)
        {
            try
            {
                return GetEntry(name) != null;
            }
            catch (StorageNotFoundException)
            {
                return false;
            }
        }

        public bool StorageExists(string name)
        {
            try
            {
                return GetStorageEntry(name) != null;
            }
            catch (StorageNotFoundException)
            {
                return false;
            }
        }

        public bool StreamExists(string name)
        {
            try
            {
                return GetStreamEntry(name) != null;
            }
            catch (StorageNotFoundException)
            {
                return false;
            }
        }

        public StorageEntry GetStorageEntry(string name)
        {
            return GetEntryHelper(name, true, false);
        }

        public StorageEntry GetStreamEntry(string name)
        {
            return GetEntryHelper(name, false, true);
        }

        public StorageEntry GetEntry(string name)
        {
            return GetEntryHelper(name, true, true);
        }

        private StorageEntry GetEntryHelper(string itemName, bool includeStorage, bool includeStream)
        {
            var entries = GetEntries(itemName);
            var item = entries.Where(x => x.Name == itemName).FirstOrDefault();
            if (item != null && includeStorage && item.IsStorage) return item;
            if (item != null && includeStream && !item.IsStorage) return item;
            throw new StorageNotFoundException(Uri, itemName);
        }

        public void SetAttributes(string name, StreamAttribute attributes)
        {
            var entry = GetEntry(name);
            try
            {
                _provider.SetAttributes(entry.Uri, attributes);
                entry.Attributes = attributes;
            }
            catch (NotSupportedException)
            {
            }
        }

        public StreamAttribute GetAttributes(string name)
        {
            var entry = GetEntry(name);
            var ret = entry.Attributes;
            return ret;
        }

        /// <summary>
        /// A Stream opened in the specified mode and path, with read/write access and not shared.
        /// </summary>
        public Stream OpenStream(string streamName, StreamMode mode, int bufferSize = 0)
        {
            return OpenStream(streamName, mode, StreamAccess.ReadWrite, StreamShare.None, bufferSize);
        }

        public Stream OpenStreamRead(string name, int bufferSize = 0)
        {
            return OpenStream(name, StreamMode.Open, StreamAccess.Read, StreamShare.Read, bufferSize);
        }

        public Stream OpenStreamWrite(string name)
        {
            return OpenStreamWrite(name, StreamShare.None);
        }

        public Stream OpenStreamWrite(string name, StreamShare share)
        {
            try
            {
                return OpenStream(name, StreamMode.Open, StreamAccess.Write, share);
            }
            catch (StorageNotFoundException)
            {
                return CreateStream(name, share);
            }

        }

        public Stream CreateStream(string name, bool overwriteExisting = false, int bufferSize = 0)
        {
            return CreateStream(name, StreamShare.None, overwriteExisting, bufferSize);
        }

        public Stream CreateStream(string name, StreamShare share, bool overwriteExisting = false, int bufferSize = 0)
        {
            if (EntryExists(name) && !overwriteExisting)
                throw new IOException("Entry already exists!");

            //try to delete the old one
            if (overwriteExisting)
                RemoveStream(name);

            //create new stream
            var result = _provider.CreateStream(name, StreamAccess.ReadWrite, share, bufferSize);
            var entry = ProviderEntryToEntry(result.Entry);
            _entryCache.TryAdd(entry.Name, entry);
            return result.Stream;
        }

        public Storage OpenStorage(string name, bool createIfNotExists)
        {
            try
            {
                return OpenStorage(name);
            }
            catch (StorageNotFoundException)
            {
                if (!createIfNotExists)
                    throw;

                return CreateStorage(name);
            }
        }

        public Storage OpenStorageByPath(string path, bool createIfNotExists = false)
        {
            path = PathUtil.RemoveLastSeparator(path);
            var parts = path.Split(SeparatorChar);

            var ret = this;
            foreach (var item in parts)
                ret = ret.OpenStorage(item, createIfNotExists);

            return ret;
        }

        public Stream OpenStreamWriteByPath(string path, StreamShare share = StreamShare.None)
        {
            var storagePath = System.IO.Path.GetDirectoryName(path);
            var storage = string.IsNullOrEmpty(storagePath) ? this: OpenStorageByPath(storagePath, true);
            return storage.OpenStreamWrite(System.IO.Path.GetFileName(path), share);
        }

        public string ReadAllText(string name)
        {
            using (var stream = OpenStreamRead(name))
            using (var sr = new StreamReader(stream))
            {
                return sr.ReadToEnd();
            }
        }

        public string ReadAllText(string name, Encoding encoding)
        {
            using (var stream = OpenStreamRead(name))
            using (var sr = new StreamReader(stream, encoding))
            {
                return sr.ReadToEnd();
            }
        }

        public void WriteAllText(string name, string text, Encoding encoding)
        {
            using (var stream = OpenStreamWrite(name))
            using (var sr = new StreamWriter(stream, encoding))
                sr.Write(text);
        }

        public void WriteAllText(string name, string text)
        {
            using (var stream = OpenStreamWrite(name))
            using (var sr = new StreamWriter(stream))
                sr.Write(text);
        }

        public byte[] ReadAllBytes(string name)
        {
            using (var stream = OpenStreamRead(name))
            using (var sr = new BinaryReader(stream))
                return sr.ReadBytes((int)stream.Length);
        }

        public void WriteAllBytes(string name, byte[] bytes)
        {
            using (var stream = OpenStreamWrite(name))
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
            var entry = new StorageEntry()
            {
                Attributes = storageProviderEntry.Attributes,
                IsStorage = storageProviderEntry.IsStorage,
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
