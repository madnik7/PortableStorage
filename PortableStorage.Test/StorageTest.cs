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
            var options = new StorageOptions
            {
                CacheTimeout = useCache ? -1 : 0
            };

            var tempPath = Path.Combine(TempPath, Guid.NewGuid().ToString());
            var storage = FileStorgeProvider.CreateRootStorage(tempPath, true, options);
            return storage;
        }

        [TestMethod]
        public void OpenStreamWrite_ByPath()
        {
            var storage = GetTempStorage();

            //check without path
            using (var ret = storage.OpenStreamWrite("filename.txt"))
                Assert.IsTrue(storage.EntryExists("filename.txt"));

            //check with path
            using (var ret = storage.OpenStreamWrite("foo1/foo2/foo3/filename2.txt"))
            {
                var storage2 = storage.OpenStorage("foo1/foo2/foo3");
                Assert.IsFalse(storage.EntryExists("filename2.txt"), "file should not exist in root");
                Assert.IsTrue(storage2.EntryExists("filename2.txt"), "file should exist in path");
            }
        }

        [TestMethod]
        public void Rename_ByPath()
        {
            var rootStorage = GetTempStorage();
            var storage = rootStorage.CreateStorage("foo2");

            storage.WriteAllText("foo3/foo4/filename1.txt", "123");
            rootStorage.Rename("foo2/foo3/foo4/filename1.txt", "filename3.txt");
            Assert.IsTrue(rootStorage.EntryExists("foo2/foo3/foo4/filename3.txt"));
            Assert.IsTrue(storage.EntryExists("foo3/foo4/filename3.txt"));
        }

        [TestMethod]
        public void Remove_ByPath()
        {
            var rootStorage = GetTempStorage();
            var storage = rootStorage.CreateStorage("foo3");

            rootStorage.WriteAllText("foo3/foo4/filename1.txt", "123");
            Assert.IsTrue(rootStorage.EntryExists("foo3/foo4/filename1.txt"));
            Assert.IsTrue(storage.EntryExists("foo4/filename1.txt"));

            storage.RemoveStream("foo4/filename1.txt");
            Assert.IsFalse(rootStorage.EntryExists("foo3/foo4/filename1.txt"));
            Assert.IsFalse(storage.EntryExists("foo4/filename1.txt"));
        }



        [ClassCleanup]
        public static void ClassCleanup() 
        {
            Directory.Delete(TempPath, true);
        }
    }
}
