using System;
using System.IO;

namespace Dopamine.Core.Audio
{
    /// <summary>
    /// A seekable stream over the encrypted audio section of an NCM file. The stream cipher keeps
    /// its index block-local (each 0x8000 byte block restarts at 0) and the key box is never
    /// mutated while decrypting, so any position can be decrypted independently.
    /// </summary>
    public class NcmStream : Stream
    {
        private const int AudioBlockSize = 0x8000;

        private readonly FileStream file;
        private readonly byte[] keyBox;
        private readonly long start;
        private readonly long length;

        private long position;

        public NcmStream(string filePath, byte[] keyBox, long start, long length)
        {
            this.file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            this.keyBox = keyBox;
            this.start = start;
            this.length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => this.length;

        public override long Position
        {
            get { return this.position; }
            set { this.position = Math.Max(0, Math.Min(value, this.length)); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.position >= this.length)
            {
                return 0;
            }

            int toRead = (int)Math.Min(count, this.length - this.position);

            this.file.Seek(this.start + this.position, SeekOrigin.Begin);

            int read = 0;
            while (read < toRead)
            {
                int n = this.file.Read(buffer, offset + read, toRead - read);
                if (n <= 0)
                {
                    break;
                }

                read += n;
            }

            for (int i = 0; i < read; i++)
            {
                int j = (int)((this.position + i) % AudioBlockSize + 1) & 0xFF;
                byte kbj = this.keyBox[j];
                buffer[offset + i] ^= this.keyBox[(kbj + this.keyBox[(kbj + j) & 0xFF]) & 0xFF];
            }

            this.position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    this.position = offset;
                    break;
                case SeekOrigin.Current:
                    this.position += offset;
                    break;
                case SeekOrigin.End:
                    this.position = this.length + offset;
                    break;
            }

            this.position = Math.Max(0, Math.Min(this.position, this.length));
            return this.position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.file.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
