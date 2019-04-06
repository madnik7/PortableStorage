using Android.OS;
using Java.Nio;
using System;
using System.IO;

namespace PortableStorage.Droid
{
    internal class ChannelStream : Stream
    {
        private Java.Nio.Channels.FileChannel Channel { get; set; }
        private readonly string _mode;
        private readonly IDisposable _stream;
        public ChannelStream(ParcelFileDescriptor parcelFileDescriptor, string mode)
        {
            _mode = mode;
            ParcelFileDescriptor = parcelFileDescriptor;

            if (mode.Contains("w"))
            {
                var outStream = new Java.IO.FileOutputStream(parcelFileDescriptor.FileDescriptor);
                Channel = outStream.Channel;
                _stream = outStream;
            }
            else
            {
                var inStream = new Java.IO.FileInputStream(parcelFileDescriptor.FileDescriptor);
                Channel = inStream.Channel;
                _stream = inStream;
            }
        }


        private bool _isDisposed = false;
        protected override void Dispose(bool disposing)
        {
            if (!disposing || _isDisposed)
                return;
            _isDisposed = true;

            (_stream as Java.IO.ICloseable)?.Close();
            _stream?.Dispose();

            ParcelFileDescriptor?.Close();
            ParcelFileDescriptor?.Dispose();

        }

        public override bool CanRead => _mode.Contains('r');
        public override bool CanWrite => _mode.Contains('w');
        public override bool CanSeek => !_mode.Contains('a');
        public override long Length => Channel.Size();
        public override long Position
        {
            get => Channel.Position();
            set => Channel.Position(value);
        }

        public ParcelFileDescriptor ParcelFileDescriptor { get; private set; }

        public override void Flush()
        {
            Channel.Force(false);
        }

        public override void SetLength(long value)
        {
            if (value > Channel.Size())
            {
                var orgPosition = Channel.Position();
                Channel.Position(value);
                Channel.Position(orgPosition);
            }
            else
            {
                Channel.Truncate(value);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long pos = offset;

            if (origin == SeekOrigin.Current)
                pos = Channel.Position() + offset;
            else if (origin == SeekOrigin.End)
                pos = Channel.Size() + offset;

            Channel.Position(pos);
            return Channel.Position();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            using (var buf = ByteBuffer.Allocate(count))
            {
                var res = Channel.Read(buf);
                buf.Position(0);

                if (buffer.Length == count && offset == 0)
                    buf.Get(buffer);
                else
                {
                    var newBuf = new byte[count];
                    buf.Get(newBuf);
                    Array.Copy(newBuf, 0, buffer, offset, count);
                }


                if (res == -1) return 0;
                return res;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            byte[] newBuf = buffer;
            if (offset != 0 || count != buffer.Length)
            {
                newBuf = new byte[count];
                Array.Copy(buffer, offset, newBuf, 0, count);
            }

            using (var buf = ByteBuffer.Wrap(newBuf))
            {
                var res = Channel.Write(buf);
            }
        }

    }
}