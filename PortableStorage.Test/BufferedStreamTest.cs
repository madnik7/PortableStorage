using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

namespace PortableStorage.Test
{
    [TestClass]
    public class BufferedStreamTest
    {
        [TestMethod]
        public void SeekAndPosition()
        {
            using var memStream = new MemoryStream();
            using var bs = new BufferedStream(memStream, 10);
            Assert.AreEqual(0, bs.Length);

            var buf = Encoding.ASCII.GetBytes("012345");
            bs.Write(buf);
            Assert.AreEqual(0, bs.Seek(0, SeekOrigin.Begin));
            Assert.AreEqual(2, bs.Seek(2, SeekOrigin.Begin));
            Assert.AreEqual(4, bs.Seek(2, SeekOrigin.Current));
            Assert.AreEqual(buf.Length, bs.Seek(0, SeekOrigin.End));

            var oldLength = buf.Length;
            bs.Write(buf, 2, 2);
            Assert.AreEqual(oldLength + 2, bs.Position);
            Assert.AreEqual(oldLength + 4, bs.Seek(2, SeekOrigin.Current));

            Assert.AreEqual(15, bs.Seek(15, SeekOrigin.Begin));
        }

        [TestMethod]
        public void ReadAndWrite()
        {
            //write and read
            using (var memStream = new MemoryStream())
            using (var bs = new BufferedStream(memStream, 10))
            {

                //less than buffer
                var buf1 = Encoding.ASCII.GetBytes("0123456789");
                bs.Write(buf1);
                bs.Seek(0, SeekOrigin.Begin);
                var buf2 = new byte[buf1.Length];
                bs.Read(buf2, 0, buf2.Length);
                Assert.AreEqual(Convert.ToBase64String(buf1), Convert.ToBase64String(buf2));

                //over buffer
                buf1 = Encoding.ASCII.GetBytes("0123456789012345");
                bs.Write(buf1);
                bs.Seek(0, SeekOrigin.Begin);
                buf2 = new byte[buf1.Length];
                bs.Read(buf2, 0, buf2.Length);
                Assert.AreEqual(Convert.ToBase64String(buf1), Convert.ToBase64String(buf2));
            }

            //seek and write
            using (var memStream = new MemoryStream())
            using (var bs = new BufferedStream(memStream, 10))
            {
                //seek and write
                var buf1 = Encoding.ASCII.GetBytes("012345.");
                bs.Seek(0, SeekOrigin.End);
                bs.Write(buf1);
                bs.Seek(0, SeekOrigin.End);
                bs.Write(buf1);
                bs.Seek(0, SeekOrigin.End);
                bs.Write(buf1);

                var buf2 = new byte[buf1.Length * 3];
                bs.Position = 0;
                bs.Read(buf2, 0, buf2.Length);
                Assert.AreEqual("012345.012345.012345.", Encoding.ASCII.GetString(buf2));
            }


            
        }

        [TestMethod]
        public void ReadBuffer()
        {
            //use read buffer
            using (var memStream = new MemoryStream())
            using (var bs = new BufferedStream(memStream, 10))
            {
                //seek and write
                var buf1 = Encoding.ASCII.GetBytes("123456789.123456789.");
                bs.Write(buf1);

                var buf2 = new byte[buf1.Length];
                bs.Position = 0;
                bs.Read(buf2, 0, buf2.Length);
                Assert.AreEqual("123456789.123456789.", Encoding.ASCII.GetString(buf2));

                bs.Position = 0;
                Array.Clear(buf2, 0, buf2.Length);
                bs.Read(buf2, 0, buf2.Length);
                Assert.AreEqual("123456789.123456789.", Encoding.ASCII.GetString(buf2));

                //try using last readed buffer
                var buf3 = new byte[10];
                bs.Position = 10;
                bs.Read(buf3, 0, buf3.Length);
                Assert.AreEqual("123456789.", Encoding.ASCII.GetString(buf3));
            }
        }
    }
}
