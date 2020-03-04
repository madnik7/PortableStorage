using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PortableStorage
{
    internal class StreamController : Stream
    {
        private readonly Stream _stream;
        private readonly StorageEntry _entry;
        public StreamController(Stream stream, StorageEntry entry)
        {
            _stream = stream;
            _entry = entry;
        }

        public override bool CanRead => _stream.CanRead;

        public override bool CanSeek => _stream.CanSeek;

        public override bool CanWrite => _stream.CanWrite;

        public override long Length => _stream.Length;

        public override long Position { get => _stream.Position; set => _stream.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
        public override void Flush() => _stream.Flush();
        protected override void Dispose(bool disposing) => _stream.Dispose();
        public override void Close() => _stream.Close();

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
            _entry.Size = value;
            _entry.LastWriteTime = DateTime.Now;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            _entry.Size += count;
            _entry.LastWriteTime = DateTime.Now;
        }

    }
}
