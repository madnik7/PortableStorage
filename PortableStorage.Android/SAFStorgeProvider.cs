using Android.Content;
using Android.Provider;
using PortableStorage.Exceptions;
using PortableStorage.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PortableStorage.Droid
{
    public class SafStorgeProvider : IStorageProvider
    {
        public Context Context { get; }
        public Android.Net.Uri AndroidUri { get; }
        public Uri Uri => AndroidUriToUri(AndroidUri);
        public bool IsGetEntriesBySearchPatternFast => false;
        public bool IsGetEntryUriByNameFast => false;
        private string _name;

        public static StorageRoot CreateStorage(Context context, Uri uri, StorageOptions storageOptions = null)
        {
            return CreateStorage(context, AndroidUriFromUri(uri), storageOptions);
        }

        public static StorageRoot CreateStorage(Context context, Android.Net.Uri androidUri, StorageOptions storageOptions = null)
        {
            var provider = new SafStorgeProvider(context, androidUri);
            return new StorageRoot(provider, storageOptions);
        }

        public SafStorgeProvider(Context context, Uri uri)
            : this(context, AndroidUriFromUri(uri))
        {
        }

        public SafStorgeProvider(Context context, Android.Net.Uri androidUri)
            : this(context, androidUri, null)
        {
        }

        private SafStorgeProvider(Context context, Android.Net.Uri androidUri, string name)
        {
            AndroidUri = androidUri;
            Context = context;
            _name = name;
        }

        public CreateStorageResult CreateStorage(string name)
        {
            var docUri = DocumentsContract.CreateDocument(Context.ContentResolver, AndroidUri, DocumentsContract.Document.MimeTypeDir, name);
            if (docUri == null)
                throw new Exception($"Could not create storage. Uri: {Uri}, name: {name}");

            var ret = new CreateStorageResult()
            {
                Storage = new SafStorgeProvider(Context, docUri, name),
                Entry = new StorageEntryBase
                {
                    Name = name,
                    LastWriteTime = DateTime.Now,
                    Size = 0,
                    IsStorage = true,
                    Uri = AndroidUriToUri(docUri),
                }
            };

            return ret;
        }

        public CreateStreamResult CreateStream(string name, StreamAccess access, StreamShare share, int bufferSize = 0)
        {
            var resolver = Context.ContentResolver;
            var androidUri = DocumentsContract.CreateDocument(resolver, AndroidUri, "application/octet-stream", name);
            var fs = OpenStream(androidUri, StreamMode.Open, access, share, bufferSize);
            var ret = new CreateStreamResult()
            {
                Stream = fs,
                EntryBase = new StorageEntryBase
                {
                    Name = name,
                    LastWriteTime = DateTime.Now,
                    Size = 0,
                    IsStorage = false,
                    Uri = AndroidUriToUri(androidUri)
                }
            };
            return ret;
        }

        public IStorageProvider OpenStorage(Uri uri)
        {
            return OpenStorage(AndroidUriFromUri(uri));
        }

        private IStorageProvider OpenStorage(Android.Net.Uri docUri)
        {
            return new SafStorgeProvider(Context, docUri);
        }

        public string Name
        {
            get
            {
                if (_name != null)
                    return _name;

                var docUri = AndroidUri;
                var projection = new string[] { DocumentsContract.Document.ColumnDisplayName };
                using (var cursor = Context.ContentResolver.Query(docUri, projection, null, null, null))
                {
                    while (cursor.MoveToNext())
                    {
                        _name = cursor.GetString(0);
                        cursor.Close();
                        return _name;
                    }
                    cursor.Close();
                }
                throw new IOException($"Could not read name. URI: {docUri}");
            }
        }

        public long GetFreeSpace()
        {
            using var fd = Context.ContentResolver.OpenFileDescriptor(AndroidUri, "r");
            var stat = Android.Systems.Os.Fstatvfs(fd.FileDescriptor);
            return stat.FBavail * stat.FBsize;
        }

        public Uri Rename(Uri uri, string desName)
        {
            return AndroidUriToUri(Rename(AndroidUriFromChildNetUri(uri), desName));
        }

        public Android.Net.Uri Rename(Android.Net.Uri docUri, string desName)
        {
            var ret = DocumentsContract.RenameDocument(Context.ContentResolver, docUri, desName);
            if (ret == null)
                throw new Exception($"Could not rename storage or stream. Uri: {Uri}");
            _name = desName;
            return ret;
        }

        public void RemoveStream(Uri uri)
        {
            RemoveStream(AndroidUriFromChildNetUri(uri));
        }

        private void RemoveStream(Android.Net.Uri docUri)
        {
            //some storage (maybe older android) does not free space till truncate the file. it is a temporaray solution
            //var stream = OpenStream(docUri, StreamMode.Truncate, StreamAccess.Write, StreamShare.None);
            //stream.Dispose();

            if (!DocumentsContract.DeleteDocument(Context.ContentResolver, docUri))
                throw new Exception($"Could not delete stream. Uri: {docUri}");
        }


        public void RemoveStorage(Uri uri)
        {
            RemoveStorage(AndroidUriFromChildNetUri(uri));
        }

        public void RemoveStorage(Android.Net.Uri docUri)
        {
            //some OTG flags does not release cause lost directory so remove directory recursively
            var subStorages = GetEntries().Where(x => x.IsStorage).Select(x => (SafStorgeProvider)OpenStorage(docUri));
            foreach (var subStorage in subStorages)
            {
                subStorage.EraseStorage();
                subStorage.Dispose();
            }

            if (!DocumentsContract.DeleteDocument(Context.ContentResolver, docUri))
                throw new Exception($"Could not delete storage. Uri: {docUri}");
        }

        private void EraseStorage()
        {
            //erase substorage
            foreach (var entry in GetEntries())
            {
                if (entry.IsStorage)
                    RemoveStorage(entry.Uri);
                //else
                  //  RemoveStream(item.Uri);
            }
        }

        public Uri GetEntryUriByName(string name)
        {
            var entries = GetEntries();
            var entry = entries.Where(x => x.Name == name).FirstOrDefault();
            if (entry != null)
                return entry.Uri;

            throw new StorageNotFoundException(Uri, name);
        }

        public Stream OpenStream(Uri uri, StreamMode mode, StreamAccess access, StreamShare share, int bufferSize = 0)
        {
            return OpenStream(AndroidUriFromChildNetUri(uri), mode, access, share, bufferSize);
        }

        private Stream OpenStream(Android.Net.Uri androidUri, StreamMode mode, StreamAccess access, StreamShare _, int bufferSize = 0)
        {
            if (access == StreamAccess.ReadWrite)
                throw new ArgumentException("StreamMode.ReadWrite does not support!");


            var resolver = Context.ContentResolver;
            string streamMode = "";
            if (bufferSize == 0) bufferSize = 4096;

            switch (mode)
            {
                case StreamMode.Append:
                    if (access == StreamAccess.Read) throw new ArgumentException("StreamMode.Append only support StreamAccess.write access.");
                    if (access == StreamAccess.Write) streamMode = "wa";
                    if (access == StreamAccess.ReadWrite) throw new ArgumentException("StreamMode.Append only support StreamAccess.write access.");
                    break;

                case StreamMode.Open:
                    if (access == StreamAccess.Read) streamMode = "r";
                    if (access == StreamAccess.Write) streamMode = "rw"; //rw instead w; because w does not support seek
                    break;

                case StreamMode.Truncate:
                    if (access == StreamAccess.Read) throw new ArgumentException("StreamMode.Truncate does not support StreamAccess.read access.");
                    if (access == StreamAccess.ReadWrite) throw new ArgumentException("StreamMode.Truncate does not support StreamAccess.readWrite access.");
                    if (access == StreamAccess.Write) streamMode = "rwt";
                    break;
            }


            var parcelFD = resolver.OpenFileDescriptor(androidUri, streamMode);
            var stream = new ChannelStream(parcelFD, streamMode);
            var ret = (Stream)new BufferedStream(stream, bufferSize);
            return ret;
        }

        public void SetAttributes(Uri uri, StreamAttributes attributes)
        {
            throw new NotSupportedException();
        }

        public StorageEntryBase[] GetEntries(string searchPattern = null)
        {
            return GetEntriesImpl(searchPattern);
        }

        private StorageEntryBase[] GetEntriesImpl(string searchPattern = null)
        {

            var itemProperties = new List<StorageEntryBase>();

            var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(AndroidUri, DocumentsContract.GetDocumentId(AndroidUri));
            var projection = new string[] {
                DocumentsContract.Document.ColumnDocumentId,
                DocumentsContract.Document.ColumnDisplayName,
                DocumentsContract.Document.ColumnMimeType,
                DocumentsContract.Document.ColumnSize,
                DocumentsContract.Document.ColumnLastModified };

            //build searchPattern
            string selection = null;
            string[] selectionArgs = null;
            var regXpattern = searchPattern != null ? Storage.WildcardToRegex(searchPattern) : null;
            //it looks doesn't supported for storage
            //if (!string.IsNullOrEmpty(searchPattern)) 
            //{
            //    selection = $"{DocumentsContract.Document.ColumnDocumentId}=?";
            //    selectionArgs = new string[] { searchPattern.Replace("*", "%") };
            //}


            //run the query
            using (var cursor = Context.ContentResolver.Query(childrenUri, projection, selection, selectionArgs, null))
            {
                while (cursor.MoveToNext())
                {
                    var documentId = cursor.GetString(0);
                    var name = cursor.GetString(1);

                    if (regXpattern != null && !Regex.IsMatch(name, regXpattern))
                        continue;

                    StreamAttributes attribute = 0;
                    if (!string.IsNullOrEmpty(name) && name[0] == '.') attribute |= StreamAttributes.Hidden;

                    itemProperties.Add(
                        new StorageEntryBase()
                        {
                            Name = name,
                            Uri = AndroidUriToUri(DocumentsContract.BuildDocumentUriUsingTree(AndroidUri, documentId)),
                            IsStorage = cursor.GetString(2) == DocumentsContract.Document.MimeTypeDir,
                            Size = long.Parse(cursor.GetString(3)),
                            LastWriteTime = cursor.GetString(4) != null ? JavaTimeStampToDateTime(double.Parse(cursor.GetString(4))) : DateTime.Now,
                            Attributes = attribute,
                        });

                    if (!string.IsNullOrEmpty(searchPattern))
                        break;
                }
                cursor.Close();
            }

            return itemProperties.ToArray();
        }

        private Android.Net.Uri AndroidUriFromChildNetUri(Uri uri)
        {
            return AndroidUriFromUri(uri);
        }

        private static DateTime JavaTimeStampToDateTime(double javaTimeStamp)
        {
            // Java timestamp is millisecods past epoch
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(Math.Round(javaTimeStamp / 1000)).ToLocalTime();
            return dtDateTime;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        private bool IsStorageUri(Android.Net.Uri docUri)
        {
            var res = Context.ContentResolver.GetType(docUri);
            return res != null && Context.ContentResolver.GetType(docUri) == DocumentsContract.Document.MimeTypeDir;
        }

        private static Android.Net.Uri AndroidUriFromUri(Uri uri)
        {
            //todo: check parent id
            return Android.Net.Uri.Parse(uri.ToString());
        }

        private static Uri AndroidUriToUri(Android.Net.Uri androidUri)
        {
            return new Uri(androidUri.ToString());
        }

        public void Dispose()
        {
        }
    }
}
