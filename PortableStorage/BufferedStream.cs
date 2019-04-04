using System;
using System.IO;

namespace PortableStorage
{
    //add the time writing this class; standard BufferedStream was too slow
    public class BufferedStream : Stream
    {
        private Stream _stream;
        private readonly int _bufferSize;
        private readonly byte[] _buf;
        private long _bufOffset = 0;
        private int _bufPos = 0;
        private int _bufUsed = 0;
        private bool _isDirty = false;

        public BufferedStream(Stream stream, int bufferSize)
        {
            _stream = stream;
            _bufferSize = bufferSize;
            _buf = new byte[bufferSize];
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;

        public override long Length
        {
            get
            {
                var byBuf = _stream.Position - _bufOffset + _bufUsed;
                return Math.Max(_stream.Length, byBuf);
            }
        }

        public override long Position
        {
            get
            {
                if (_bufUsed > 0)
                    return _bufOffset + _bufPos;
                return _stream.Position;
            }
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush()
        {
            FlushDirty();
            _stream.Flush();
        }

        private void FlushDirty()
        {
            if (_isDirty && _bufUsed > 0)
            {
                if (_stream.Position != _bufOffset)
                    _stream.Seek(_bufOffset, SeekOrigin.Begin);
                _stream.Write(_buf, 0, _bufUsed);
                _bufOffset = Position;
            }
            _bufPos = 0;
            _bufUsed = 0;
            Array.Clear(_buf, 0, _bufUsed);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            //read already readed
            var bufReadCount = Math.Min(count, _bufUsed - _bufPos);
            Buffer.BlockCopy(_buf, _bufPos, buffer, offset, bufReadCount);
            offset += bufReadCount;
            _bufPos += bufReadCount;
            var remain = count - bufReadCount;

            if (remain >= _bufferSize)
            {
                FlushDirty();
                _bufOffset = _stream.Position;
                return _stream.Read(buffer, offset, remain) + bufReadCount;
            }
            else if (remain > 0)
            {
                FlushDirty();
                _bufOffset = _stream.Position;
                _bufUsed = _stream.Read(_buf, 0, _bufferSize);
                if (_bufUsed > 0)
                    return Read(buffer, offset, remain) + bufReadCount;
            }

            return bufReadCount;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count > 0) _isDirty = true;
            var bufWriteCount = Math.Min(count, _bufferSize - _bufPos);
            Buffer.BlockCopy(buffer, offset, _buf, _bufPos, bufWriteCount);
            offset += bufWriteCount;
            _bufPos += bufWriteCount;
            _bufUsed = Math.Max(_bufUsed, _bufPos);
            var remain = count - bufWriteCount;

            if (remain >= _bufferSize)
            {
                FlushDirty();
                _stream.Write(buffer, offset, remain);
                _bufOffset = _stream.Position;
            }
            else if (remain > 0)
            {
                FlushDirty();
                Write(buffer, offset, remain);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            //calcualte required position
            var pos = offset;
            if (origin == SeekOrigin.Current) pos = Position + offset;
            else if (origin == SeekOrigin.End) pos = Length + offset;

            //check is within buffer range
            if (pos >= _bufOffset && pos < _bufOffset + _bufUsed)
            {
                _bufPos = (int)(pos - _bufOffset);
                return _bufPos;
            }
            else
            {
                FlushDirty();
                if (_stream.Position != offset)
                    _bufOffset = _stream.Seek(offset, origin);
                return _bufOffset;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                FlushDirty();
            base.Dispose(disposing);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

    }
}