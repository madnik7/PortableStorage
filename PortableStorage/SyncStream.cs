using System;
using System.IO;

namespace PortableStorage
{
    public class SyncStream : Stream
    {
        private readonly Stream _stream;
        private long _position;

        public SyncStream(Stream stream, bool keepCurrentPosition = false)
        {
            _stream = stream ?? throw new ArgumentNullException("stream");
            _position = keepCurrentPosition ? _stream.Position : 0;
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length
        {
            get
            {
                lock (_stream)
                    return _stream.Length;
            }
        }

        public override void Flush()
        {
            lock (_stream)
                _stream.Flush();
        }
        public override void SetLength(long value)
        {
            lock (_stream)
                _stream.SetLength(value);
        }

        public override long Position
        {
            get
            {
                lock (_stream)
                    return _position;
            }
            set
            {
                lock (_stream)
                {
                    if (_position == value)
                        return;

                    // check is seekable
                    if (!CanSeek)
                        throw new NotSupportedException();

                    // set next offset
                    _position = value;
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (_stream)
            {
                long newPosition;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        newPosition = offset;
                        break;

                    case SeekOrigin.Current:
                        newPosition = offset + _position;
                        break;

                    case SeekOrigin.End:
                        newPosition = offset + Length;
                        break;

                    default:
                        throw new NotSupportedException();
                }

                if (_position == newPosition)
                    return _position;

                // check is seekable
                if (!CanSeek)
                    throw new NotSupportedException();

                // set next offset
                _position = newPosition;
                return newPosition;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_stream)
            {
                _stream.Position = _position;
                var ret = _stream.Read(buffer, offset, count);
                _position += ret;
                return ret;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_stream)
            {
                _stream.Position = _position;
                _stream.Write(buffer, offset, count);
                _position += count;
            }
        }
    }
}
