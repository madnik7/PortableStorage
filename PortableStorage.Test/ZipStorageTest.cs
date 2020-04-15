using Microsoft.VisualStudio.TestTools.UnitTesting;
using PortableStorage.Providers;
using System;
using System.IO;
using System.IO.Compression;

namespace PortableStorage.Test
{
    [TestClass]
    public class ZipStorageTest
    {
        private static string TempPath => Path.Combine(Path.GetTempPath(), "_test_portablestroage_zip");

        private StorageRoot GetTempStorage(bool useCache = true)
        {
            var options = new StorageOptions
            {
                CacheTimeout = useCache ? -1 : 0
            };

            var tempPath = Path.Combine(TempPath, Guid.NewGuid().ToString());
            var storage = FileStorgeProvider.CreateStorage(tempPath, true, options);
            return storage;

        }

        private Stream GetTempZipStream()
        {
            var buf = new byte[10000];

            using (var zipArchive = new ZipArchive(new MemoryStream(buf), ZipArchiveMode.Create))
            {
                AddToZipArchive(zipArchive, "file1.txt", "file1.txt contents.");
                AddToZipArchive(zipArchive, "/file4.txt", "file4.txt contents.");
                AddToZipArchive(zipArchive, "folder1/file2.txt", "file2.txt contents.");
                AddToZipArchive(zipArchive, "\\folder_backslash\\file.txt", "file.txt contents.");
                AddToZipArchive(zipArchive, "folder1/folder2/file3.txt", "file3.txt contents.");
            }

            return new MemoryStream(buf);
        }


        [TestMethod]
        public void Open_zip_storage_and_stream_by_provider()
        {
            using var zipStream = GetTempZipStream();
            using var zipStorage = ZipStorgeProvider.CreateStorage(zipStream);
            Assert.IsTrue(zipStorage.StreamExists("file1.txt"));
            Assert.IsTrue(zipStorage.StreamExists("file4.txt"));
            Assert.IsTrue(zipStorage.StreamExists("folder1/file2.txt"));
            Assert.IsTrue(zipStorage.StreamExists("folder_backslash/file.txt"));
            Assert.IsTrue(zipStorage.StorageExists("folder1/folder2"));
            Assert.IsFalse(zipStorage.StorageExists("folder1/folder2/file3.txt"));

            var str = zipStorage.OpenStorage("folder1").OpenStorage("folder2").ReadAllText("file3.txt");
            Assert.AreEqual("file3.txt contents.", str, "unexpected text has been readed");
        }

        private static void AddToZipArchive(ZipArchive zipArchive, string path, string text)
        {
            //css/css.txt
            using var stream = zipArchive.CreateEntry(path).Open();
            using var writer = new StreamWriter(stream);
            writer.Write(text);
        }

        [TestMethod]
        public void Open_zip_storage_and_stream_by_ZipArchive_without_directory_entry()
        {
            var buf = new byte[10000];
            using (var zipArchive = new ZipArchive(new MemoryStream(buf), ZipArchiveMode.Create))
            {
                AddToZipArchive(zipArchive, "folder1/folder1/folder1/file1.zip", "z");
                AddToZipArchive(zipArchive, "folder1/folder1/folder1/file2.zip", "z");
            }

            using var zipStream = new MemoryStream(buf);
            var zipStorage = ZipStorgeProvider.CreateStorage(zipStream);
            Assert.IsTrue(zipStorage.StorageExists("folder1"));
            Assert.IsTrue(zipStorage.StorageExists("folder1/folder1"));
            Assert.IsTrue(zipStorage.StorageExists("folder1/folder1"));
            Assert.IsTrue(zipStorage.StorageExists("folder1/folder1/folder1"));
            Assert.IsTrue(zipStorage.StreamExists("folder1/folder1/folder1/file1.zip"));
            Assert.IsTrue(zipStorage.StreamExists("folder1/folder1/folder1/file2.zip"));

            var str = zipStorage.ReadAllText("folder1/folder1/folder1/file2.zip");
            Assert.AreEqual("z", str, "unexpected text has been readed");
        }

        [TestMethod]
        public void Open_zip_storage_and_stream_by_virtualprovider()
        {
            using var storage = GetTempStorage();
            var folder1 = storage.CreateStorage("folder1");

            using (var zipStreamSrc = GetTempZipStream())
            using (var zipStreamDest = folder1.CreateStream("test.zip"))
                zipStreamSrc.CopyTo(zipStreamDest);

            Assert.IsTrue(storage.StreamExists("folder1/test.zip"));
            Assert.IsTrue(storage.StorageExists("folder1/test.zip/folder1"));
            Assert.IsTrue(storage.StreamExists("folder1/test.zip/folder1/file2.txt"));
            Assert.AreEqual("file3.txt contents.", storage.ReadAllText("folder1/test.zip/folder1/folder2/file3.txt"), "unexpected text has been readed");
            
            // check IsVirtual
            Assert.IsFalse(storage.OpenStorage("folder1").IsVirtual);
            Assert.IsTrue(storage.OpenStorage("folder1/test.zip").IsVirtual);
            Assert.IsTrue(storage.OpenStorage("folder1/test.zip/folder1").IsVirtual);
            Assert.IsTrue(storage.OpenStorage("folder1/test.zip/folder1/folder2").IsVirtual);
        }

        [TestMethod]
        public void Map_virtualStorage__after_Rename()
        {
            using var storage = GetTempStorage();
            var folder1 = storage.CreateStorage("folder1");

            using (var zipStreamSrc = GetTempZipStream())
            using (var zipStreamDest = folder1.CreateStream("test.zz"))
                zipStreamSrc.CopyTo(zipStreamDest);

            Assert.IsFalse(storage.GetEntry("folder1/test.zz").IsVirtualStorage);
            Assert.IsFalse(storage.GetEntry("folder1/test.zz").IsStorage);

            //rename to zip
            storage.Rename("folder1/test.zz", "test.zip");
            Assert.IsTrue(storage.GetEntry("folder1/test.zip").IsVirtualStorage, "Virtual Storage doesn't map!");
            Assert.IsTrue(storage.GetEntry("folder1/test.zip").IsStorage, "Virtual Storage doesn't map!");
        }


        [TestMethod]
        public void Open_zip_storage_by_resource()
        {
            using var zipStream = new MemoryStream(Resource.TestZip);
            using var zipStorage = ZipStorgeProvider.CreateStorage(zipStream);
            var text = zipStorage.ReadAllText("Folder1/File1.txt");
            Assert.AreEqual("File1 Text.", text, "The sample file content couldn't be readed properly!");
            Assert.IsTrue(zipStorage.StreamExists("Root.txt"));
            Assert.IsTrue(zipStorage.StorageExists("Folder1"));
        }


        [TestMethod]
        public void Dispose_zip_storage_by_virtual_folder()
        {
            using var storage = GetTempStorage();
            var folder1 = storage.CreateStorage("folder1");
            using (var zipStreamSrc = GetTempZipStream())
            using (var zipStreamDest = folder1.CreateStream("test.zip"))
                zipStreamSrc.CopyTo(zipStreamDest);

            var stream = storage.OpenStreamRead("folder1/test.zip/folder1/folder2/file3.txt");
            stream.Dispose();

            Assert.IsTrue(storage.StreamExists("folder1/test.zip"));
            storage.DeleteStream("folder1/test.zip");
        }

        [TestMethod]
        public void Deleted_and_Rename_Mapped_VirtualFolder()
        {
            using var storage = GetTempStorage();
            var folder1 = storage.CreateStorage("folder1");
            using (var zipStreamSrc = GetTempZipStream())
            using (var zipStreamDest = folder1.CreateStream("test.zip"))
                zipStreamSrc.CopyTo(zipStreamDest);

            var stream = storage.OpenStreamRead("folder1/test.zip/folder1/folder2/file3.txt");

            //try delete the zip file while another stream is open
            storage.Rename("folder1/test.zip", "test2.zip");

            var stream2 = storage.OpenStreamRead("folder1/test2.zip/folder1/folder2/file3.txt");
            storage.DeleteStorage("folder1");
        }


        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);
        }
    }
}
