using Microsoft.VisualStudio.TestTools.UnitTesting;
using PortableStorage.Providers;
using System;
using System.IO;

namespace PortableStorage.Test
{
    [TestClass]
    public class StorageTest
    {
        private static string TempPath => Path.Combine(Path.GetTempPath(), "_test_portablestroage");

        private Storage GetTempStorage(bool useCache = true)
        {
            var tempPath = Path.Combine(TempPath, Guid.NewGuid().ToString());
            var storage = FileStorgeProvider.CreateStorage(tempPath, true, useCache ? 0 : -1);
            return storage;

        }

        [TestMethod]
        public void OpenStreamWriteByPath()
        {
            var storage = GetTempStorage();

            //check without path
            using (var ret = storage.OpenStreamWriteByPath("filenme.txt"))
                Assert.IsTrue(storage.EntryExists("filenme.txt"));

            //check with path
            using (var ret = storage.OpenStreamWriteByPath("foo1/foo2/foo3/filenme2.txt"))
            {
                var storage2 = storage.OpenStorageByPath("foo1/foo2/foo3");
                Assert.IsFalse(storage.EntryExists("filenme2.txt"), "file should not exist in root");
                Assert.IsTrue(storage2.EntryExists("filenme2.txt"), "file should exist in path");
            }
        }

        [ClassCleanup]
        public static void ClassCleanup() 
        {
            Directory.Delete(TempPath, true);
        }
    }
}
