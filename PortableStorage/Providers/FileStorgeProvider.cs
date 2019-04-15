using PortableStorage.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;

namespace PortableStorage.Providers
{
    public class FileStorgeProvider : IStorageProvider
    {
        public Uri Uri => new Uri(PathUtil.AddLastSeparator(SystemPath));
        public string SystemPath { get; private set; }
        public bool IsGetEntriesBySearchPatternFast => true;
        public bool IsGetEntryUriByNameFast => true;

        public static Storage CreateStorage(string path, bool createIfNotExists, StorageOptions storageOptions = null)
        {
            var provider = new FileStorgeProvider(path, createIfNotExists);
            var ret = new Storage(provider, storageOptions);
            return ret;
        }

        public FileStorgeProvider(string path, bool createIfNotExists = false)
        {
            SystemPath = PathUtil.AddLastSeparator(path);

            //check existance
            if (!Directory.Exists(path))
            {
                if (createIfNotExists)
                    Directory.CreateDirectory(path);
                else
                    throw new StorageNotFoundException(PathToUri(path));
            }
        }

        public string Name => Path.GetFileName(PathUtil.RemoveLastSeparator(SystemPath));

        public long GetFreeSpace()
        {
            var drive = new DriveInfo(SystemPath);
            var driveInfo = new DriveInfo(drive.Name);
            var ret = driveInfo.AvailableFreeSpace;
            return ret;
        }

        public CreateStorageResult CreateStorage(string name)
        {
            var folderPath = Path.Combine(SystemPath, name);
            Directory.CreateDirectory(folderPath);
            var ret = new CreateStorageResult()
            {
                Entry = StorageProviderEntryFromPath(folderPath),
                Storage = new FileStorgeProvider(folderPath)
            };
            return ret;
        }

        public CreateStreamResult CreateStream(string name, StreamAccess access, StreamShare share, int bufferSize)
        {
            var filePath = Path.Combine(SystemPath, name);
            var fs = OpenStream(filePath, FileMode.Create, access, share, bufferSize);
            var ret = new CreateStreamResult()
            {
                Entry = StorageProviderEntryFromPath(filePath),
                Stream = fs
            };
            return ret;
        }

        public IStorageProvider OpenStorage(Uri uri)
        {
            var folderPath = PathFromUri(uri);
            if (!Directory.Exists(folderPath))
                throw new StorageNotFoundException(new Uri(folderPath));
            var ret = new FileStorgeProvider(folderPath);
            return ret;
        }

        StorageEntryBase[] IStorageProvider.GetEntries(string searchPattern)
        {
            var fsEntries = string.IsNullOrEmpty(searchPattern) ? Directory.GetFileSystemEntries(SystemPath) : Directory.GetFileSystemEntries(SystemPath, searchPattern);

            var ret = new List<StorageEntryBase>(fsEntries.Length);
            foreach (var fsEntry in fsEntries)
            {
                var entry = StorageProviderEntryFromPath(fsEntry);
                ret.Add(entry);
            }
            return ret.ToArray();
        }

        public Uri Rename(Uri uri, string desName)
        {
            var srcPath = PathFromUri(uri);
            var desPath = Path.Combine(SystemPath, desName);
            File.Move(srcPath, desPath);
            return PathToUri(desPath);
        }

        public Stream OpenStream(Uri uri, StreamMode mode, StreamAccess access, StreamShare share, int bufferSize)
        {
            var fmode = FileMode.Append;
            switch (mode)
            {
                case StreamMode.Append: fmode = FileMode.Append; break;
                case StreamMode.Open: fmode = FileMode.Open; break;
                case StreamMode.Truncate: fmode = FileMode.Truncate; break;
            }

            var filePath = PathFromUri(uri);
            return OpenStream(filePath, fmode, access, share, bufferSize);
        }

        private Stream OpenStream(string filePath, FileMode fmode, StreamAccess access, StreamShare share, int bufferSize)
        {
            var faccess = FileAccess.Read;
            switch (access)
            {
                case StreamAccess.Read: faccess = FileAccess.Read; break;
                case StreamAccess.ReadWrite: faccess = FileAccess.ReadWrite; break;
                case StreamAccess.Write: faccess = FileAccess.Write; break;
            }

            FileShare fshare = FileShare.None;
            switch (share)
            {
                case StreamShare.None: fshare = FileShare.None; break;
                case StreamShare.Read: fshare = FileShare.Read; break;
                case StreamShare.ReadWrite: fshare = FileShare.ReadWrite; break;
                case StreamShare.Write: fshare = FileShare.Write; break;
            }

            return File.Open(filePath, fmode, faccess, fshare);
        }

        public void RemoveStream(Uri uri)
        {
            var filePath = PathFromUri(uri);
            File.Delete(filePath);
        }

        public void RemoveStorage(Uri uri)
        {
            var folderPath = PathFromUri(uri);
            Directory.Delete(folderPath, true);
        }

        public void SetAttributes(Uri uri, StreamAttribute attributes)
        {
            FileAttributes fattr = 0;
            var filePath = PathFromUri(uri);
            if (attributes.HasFlag(StreamAttribute.Hidden)) fattr |= FileAttributes.Hidden;
            if (attributes.HasFlag(StreamAttribute.System)) fattr |= FileAttributes.System;
            File.SetAttributes(filePath, fattr);
        }


        public StreamAttribute GetAttributes(Uri uri)
        {
            StreamAttribute attr = 0;

            var filePath = PathFromUri(uri);
            var fileAttr = File.GetAttributes(filePath);
            if (fileAttr.HasFlag(FileAttributes.Hidden)) attr |= StreamAttribute.Hidden;
            if (fileAttr.HasFlag(FileAttributes.System)) attr |= StreamAttribute.System;

            return attr;
        }

        private string PathFromUri(Uri uri)
        {
            var name = Path.GetFileName(uri.LocalPath);
            if (name.Contains(".."))
                throw new ArgumentException("parent access has been rejected!", "uri");
            return Path.Combine(SystemPath, name);
        }

        private static Uri PathToUri(string path)
        {
            return new Uri(path);
        }

        private static StorageEntryBase StorageProviderEntryFromPath(string path)
        {
            return StorageProviderEntryFromFileInfo(new FileInfo(path));
        }

        private static StorageEntryBase StorageProviderEntryFromFileInfo(FileInfo fileInfo)
        {
            StreamAttribute attr = 0;
            var fileAttr = fileInfo.Attributes;
            if (fileAttr.HasFlag(FileAttributes.Hidden)) attr |= StreamAttribute.Hidden;
            if (fileAttr.HasFlag(FileAttributes.System)) attr |= StreamAttribute.System;

            var ret = new StorageEntryBase()
            {
                Uri = new Uri(fileInfo.FullName),
                Attributes = attr,
                IsStorage = fileAttr.HasFlag(FileAttributes.Directory),
                Name = fileInfo.Name,
                LastWriteTime = fileInfo.LastWriteTime,
                Size = fileAttr.HasFlag(FileAttributes.Directory) ? 0 : fileInfo.Length
            };

            return ret;
        }
    }
}
