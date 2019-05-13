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

        private Storage GetTempStorage(StorageOptions options = null)
        {
            var tempPath = Path.Combine(TempPath, Guid.NewGuid().ToString());
            var storage = FileStorgeProvider.CreateStorage(tempPath, true, options);
            return storage;
        }

        [TestMethod]
        public void OpenStreamWrite_ByPath()
        {
            using (var storage = GetTempStorage())
            {

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
        }

        [TestMethod]
        public void Rename_ByPath()
        {
            using (var rootStorage = GetTempStorage())
            {
                var storage = rootStorage.CreateStorage("foo2");

                storage.WriteAllText("foo3/foo4/filename1.txt", "123");
                rootStorage.Rename("foo2/foo3/foo4/filename1.txt", "filename3.txt");
                Assert.IsTrue(rootStorage.EntryExists("foo2/foo3/foo4/filename3.txt"));
                Assert.IsTrue(storage.EntryExists("foo3/foo4/filename3.txt"));
            }
        }

        [TestMethod]
        public void Remove_ByPath()
        {
            using (var rootStorage = GetTempStorage())
            {
                var storage = rootStorage.CreateStorage("foo3");

                rootStorage.WriteAllText("foo3/foo4/filename1.txt", "123");
                Assert.IsTrue(rootStorage.EntryExists("foo3/foo4/filename1.txt"));
                Assert.IsTrue(storage.EntryExists("foo4/filename1.txt"));

                storage.RemoveStream("foo4/filename1.txt");
                Assert.IsFalse(rootStorage.EntryExists("foo3/foo4/filename1.txt"));
                Assert.IsFalse(storage.EntryExists("foo4/filename1.txt"));
            }
        }

        [TestMethod]
        public void Storage_Path()
        {
            using (var rootStorage = GetTempStorage())
            {
                var storage = rootStorage.CreateStorage("foo3");

                try
                {
                    var test = rootStorage.OpenStorage("/");
                    Assert.Fail("ArgumentException was expected!");
                }
                catch (ArgumentException)
                {
                    // OK
                }
            }
        }

        [TestMethod]
        public void Create_stream_overwrite_existing()
        {
            using (var rootStorage = GetTempStorage())
            {
                using (var stream = rootStorage.CreateStream("Test/foo1.txt", true))
                    stream.WriteByte(10);

                using (var stream2 = rootStorage.OpenStreamRead("Test/foo1.txt"))
                    Assert.AreEqual(stream2.ReadByte(), 10);
            }
        }

        [TestMethod]
        public void Case_sensitive()
        {
            using (var rootStorage = GetTempStorage(new StorageOptions() { IgnoreCase = false }))
            {
                rootStorage.WriteAllText("foo1/filename1.txt", "123");
                Assert.IsTrue(rootStorage.EntryExists("foo1/filename1.txt"));
                Assert.IsFalse(rootStorage.EntryExists("foo1/Filename1.txt"));
                Assert.IsFalse(rootStorage.EntryExists("Foo1/filename1.txt"));
            }

            using (var rootStorage = GetTempStorage(new StorageOptions() { IgnoreCase = true }))
            {
                rootStorage.WriteAllText("foo1/filename1.txt", "123");
                Assert.IsTrue(rootStorage.EntryExists("foo1/filename1.txt"));
                Assert.IsTrue(rootStorage.EntryExists("foo1/Filename1.txt"));
                Assert.IsTrue(rootStorage.EntryExists("Foo1/filename1.txt"));
            }
        }

        [TestMethod]
        public void WriteAllText_overwrite_oldfile()
        {
            using (var rootStorage = GetTempStorage())
            {
                var path = "foo1/filename1.txt";
                rootStorage.WriteAllText(path, "123456789");
                rootStorage.WriteAllText(path, "123");
                Assert.AreEqual(rootStorage.ReadAllText(path), "123");
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            Directory.Delete(TempPath, true);
        }
    }
}
