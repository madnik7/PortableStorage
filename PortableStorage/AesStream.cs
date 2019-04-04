using System;
using System.IO;
using System.Security.Cryptography;

namespace PortableStorage
{
    public class AesStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly AesManaged _aes;
        private readonly ICryptoTransform _encryptor;
        private const int _keySize = 128;
        public bool AutoDisposeBaseStream { get; set; } = true;

        /// <param name="salt">//** WARNING **: MUST be unique for each stream otherwise there is NO security</param>
        public AesStream(Stream baseStream, string password, byte[] salt)
        {
            _baseStream = baseStream;
            using (var key = new PasswordDeriveBytes(password, salt))
            {
                _aes = new AesManaged
                {
                    KeySize = _keySize,
                    Mode = CipherMode.ECB,
                    Padding = PaddingMode.None,
                    IV = new byte[16], //zero buffer is adequate since we have to use new salt for each stream
                    Key = key.GetBytes(_keySize / 8)
                };
                _encryptor = _aes.CreateEncryptor(_aes.Key, _aes.IV);
            }
        }

        private void Cipher(byte[] buffer, int offset, int count, long streamPos)
        {
            //find block number
            var blockSizeInByte = _aes.BlockSize / 8;
            var blockNumber = (streamPos / blockSizeInByte) + 1;
            var keyPos = streamPos % blockSizeInByte;

            //buffer
            var outBuffer = new byte[blockSizeInByte];
            var nonce = new byte[blockSizeInByte];
            var init = false;

            for (int i = offset; i < count; i++)
            {
                //encrypt the nonce to form next xor buffer (unique key)
                if (!init || (keyPos % blockSizeInByte) == 0)
                {
                    BitConverter.GetBytes(blockNumber).CopyTo(nonce, 0);
                    _encryptor.TransformBlock(nonce, 0, nonce.Length, outBuffer, 0);
                    if (init) keyPos = 0;
                    init = true;
                    blockNumber++;
                }
                buffer[i] ^= outBuffer[keyPos]; //simple XOR with generated unique key
                keyPos++;
            }
        }

        public override bool CanRead { get { return _baseStream.CanRead; } }
        public override bool CanSeek { get { return _baseStream.CanSeek; } }
        public override bool CanWrite { get { return _baseStream.CanWrite; } }
        public override long Length { get { return _baseStream.Length; } }
        public override long Position { get { return _baseStream.Position; } set { _baseStream.Position = value; } }
        public override void Flush() { _baseStream.Flush(); }
        public override void SetLength(long value) { _baseStream.SetLength(value); }
        public override long Seek(long offset, SeekOrigin origin) { return _baseStream.Seek(offset, origin); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var streamPos = Position;
            var ret = _baseStream.Read(buffer, offset, count);
            Cipher(buffer, offset, count, streamPos);
            return ret;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Cipher(buffer, offset, count, Position);
            _baseStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _encryptor?.Dispose();
                _aes?.Dispose();
                if (AutoDisposeBaseStream)
                    _baseStream?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
