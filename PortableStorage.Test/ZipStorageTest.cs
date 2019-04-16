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

        private Storage GetTempStorage(bool useCache = true)
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

            using (var zip = new ZipArchive(new MemoryStream(buf), ZipArchiveMode.Create))
            {

                using (var writer = new StreamWriter(zip.CreateEntry("file1.txt").Open()))
                    writer.Write("file1.txt  contents.");

                using (var writer = new StreamWriter(zip.CreateEntry("/file4.txt").Open()))
                    writer.Write("file1.txt  contents.");

                using (var writer = new StreamWriter(zip.CreateEntry("folder1/file2.txt").Open()))
                    writer.Write("file2.txt contents.");

                using (var writer = new StreamWriter(zip.CreateEntry("folder1/folder2/file3.txt").Open()))
                    writer.Write("file3.txt contents.");
            }

            return new MemoryStream(buf);
        }


        [TestMethod]
        public void Open_zip_storage_and_stream_by_provider()
        {
            var zipStream = GetTempZipStream();
            var zipStorage = ZipStorgeProvider.CreateStorage(zipStream);

            Assert.IsTrue(zipStorage.StreamExists("file1.txt"));
            Assert.IsTrue(zipStorage.StreamExists("file4.txt"));
            Assert.IsTrue(zipStorage.StreamExists("folder1/file2.txt"));
            Assert.IsTrue(zipStorage.StorageExists("folder1/folder2"));
            Assert.IsFalse(zipStorage.StorageExists("folder1/folder2/file3.txt"));

            var str = zipStorage.OpenStorage("folder1").OpenStorage("folder2").ReadAllText("file3.txt");
            Assert.AreEqual(str, "file3.txt contents.", "unexpected text has been readed");
        }

        [TestMethod]
        public void Open_zip_storage_and_stream_by_virtualprovider()
        {
            var storage = GetTempStorage();
            var folder1 = storage.CreateStorage("folder1");

            using (var zipStreamSrc = GetTempZipStream())
            using (var zipStreamDest = folder1.CreateStream("test.zip"))
                zipStreamSrc.CopyTo(zipStreamDest);

            Assert.IsTrue(storage.StorageExists("folder1/test.zip"));
            Assert.IsTrue(storage.StreamExists("folder1/test.zip"));
            Assert.IsTrue(storage.StorageExists("folder1/test.zip/folder1"));
            Assert.IsTrue(storage.StreamExists("folder1/test.zip/folder1/file2.txt"));
            Assert.AreEqual(storage.ReadAllText("folder1/test.zip/folder1/folder2/file3.txt"), "file3.txt contents.", "unexpected text has been readed");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);
        }
    }
}
