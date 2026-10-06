using Dopamine.Core.Base;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dopamine.Core.Audio
{
    /// <summary>
    /// Parses a NetEase Cloud Music (NCM) container and decrypts its payload. An NCM file is not
    /// a codec: it wraps an already encoded FLAC or MP3 stream, plus metadata and cover art, and
    /// encrypts everything with AES-128-ECB (key/metadata) and a modified RC4 stream (audio).
    /// </summary>
    public class NcmFile
    {
        private const uint Magic1 = 0x4E455443; // "CTEN"
        private const uint Magic2 = 0x4D414446; // "FDAM"
        private const int AudioBlockSize = 0x8000;

        private static readonly byte[] CoreKey = { 0x68, 0x7A, 0x48, 0x52, 0x41, 0x6D, 0x73, 0x6F, 0x35, 0x6B, 0x49, 0x6E, 0x62, 0x61, 0x78, 0x57 };
        private static readonly byte[] ModifyKey = { 0x23, 0x31, 0x34, 0x6C, 0x6A, 0x6B, 0x5F, 0x21, 0x5C, 0x5D, 0x26, 0x30, 0x55, 0x3C, 0x27, 0x28 };
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private readonly byte[] keyBox = new byte[256];

        public NcmFile(string filePath)
        {
            this.FilePath = filePath;
            this.Metadata = new NcmMetadata();
            this.Parse();
        }

        public string FilePath { get; }

        public NcmMetadata Metadata { get; }

        /// <summary>Extension of the wrapped audio: ".flac" or ".mp3".</summary>
        public string PayloadExtension { get; private set; } = FileFormats.FLAC;

        public long AudioOffset { get; private set; }

        public long AudioLength { get; private set; }

        public static bool IsNcmFile(string filePath)
        {
            return !string.IsNullOrEmpty(filePath)
                && string.Equals(Path.GetExtension(filePath), ".ncm", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Opens the decrypted payload (a plain FLAC or MP3 stream) for playback.</summary>
        public Stream OpenPayload()
        {
            return new NcmStream(this.FilePath, this.keyBox, this.AudioOffset, this.AudioLength);
        }

        private void Parse()
        {
            using (var stream = new FileStream(this.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != Magic1 || reader.ReadUInt32() != Magic2)
                {
                    throw new InvalidDataException("Not a NetEase Cloud Music (NCM) file.");
                }

                stream.Seek(2, SeekOrigin.Current);

                int keyLength = checked((int)reader.ReadUInt32());
                byte[] keyData = reader.ReadBytes(keyLength);
                for (int i = 0; i < keyData.Length; i++)
                {
                    keyData[i] ^= 0x64;
                }

                byte[] decryptedKey = AesEcbDecrypt(CoreKey, keyData);

                // 17 bytes "neteasecloudmusic" followed by the 16 byte RC4 key.
                this.BuildKeyBox(decryptedKey, 17, decryptedKey.Length - 17);

                int metaLength = checked((int)reader.ReadUInt32());

                if (metaLength > 0)
                {
                    byte[] metaData = reader.ReadBytes(metaLength);
                    for (int i = 0; i < metaData.Length; i++)
                    {
                        metaData[i] ^= 0x63;
                    }

                    // 22 bytes "163 key(Don't modify):" followed by Base64.
                    string base64 = Encoding.UTF8.GetString(metaData, 22, metaData.Length - 22);
                    byte[] decryptedMeta = AesEcbDecrypt(ModifyKey, Convert.FromBase64String(base64));

                    // 6 bytes "music:" followed by JSON.
                    this.ParseMetadataJson(Encoding.UTF8.GetString(decryptedMeta, 6, decryptedMeta.Length - 6));
                }

                stream.Seek(5, SeekOrigin.Current); // skip CRC32 (4) + image version (1)

                int coverFrameLength = checked((int)reader.ReadUInt32());
                int coverLength = checked((int)reader.ReadUInt32());

                if (coverLength > 0)
                {
                    byte[] cover = reader.ReadBytes(coverLength);
                    this.Metadata.Cover = cover;
                    this.Metadata.CoverMimeType = this.StartsWith(cover, PngMagic) ? "image/png" : "image/jpeg";
                }

                stream.Seek(coverFrameLength - coverLength, SeekOrigin.Current);

                this.AudioOffset = stream.Position;
                this.AudioLength = stream.Length - this.AudioOffset;

                this.DetectFormat(stream);
            }
        }

        private void DetectFormat(FileStream stream)
        {
            byte[] probe = new byte[Math.Min(AudioBlockSize, this.AudioLength)];

            stream.Seek(this.AudioOffset, SeekOrigin.Begin);

            int read = 0;
            while (read < probe.Length)
            {
                int n = stream.Read(probe, read, probe.Length - read);
                if (n <= 0) break;
                read += n;
            }

            for (int i = 0; i < read; i++)
            {
                int j = (i + 1) & 0xFF;
                byte kbj = this.keyBox[j];
                probe[i] ^= this.keyBox[(kbj + this.keyBox[(kbj + j) & 0xFF]) & 0xFF];
            }

            bool isMp3 = read >= 3 && probe[0] == 0x49 && probe[1] == 0x44 && probe[2] == 0x33; // "ID3"
            this.PayloadExtension = isMp3 ? FileFormats.MP3 : FileFormats.FLAC;
        }

        private void BuildKeyBox(byte[] key, int keyOffset, int keyLength)
        {
            for (int i = 0; i < 256; i++)
            {
                this.keyBox[i] = (byte)i;
            }

            byte swap = 0;
            byte c = 0;
            byte lastByte = 0;
            int offset = keyOffset;

            for (int i = 0; i < 256; i++)
            {
                swap = this.keyBox[i];
                c = unchecked((byte)((swap + lastByte + key[offset++]) & 0xFF));

                if (offset >= keyOffset + keyLength)
                {
                    offset = keyOffset;
                }

                this.keyBox[i] = this.keyBox[c];
                this.keyBox[c] = swap;
                lastByte = c;
            }
        }

        private void ParseMetadataJson(string json)
        {
            try
            {
                JObject root = JObject.Parse(json);

                this.Metadata.Name = (string)root["musicName"] ?? string.Empty;
                this.Metadata.Format = (string)root["format"] ?? string.Empty;
                this.Metadata.Bitrate = (int?)root["bitrate"] ?? 0;
                this.Metadata.Duration = (int?)root["duration"] ?? 0;
                this.Metadata.Album = ReadFirstValue(root["album"]);
                this.Metadata.AlbumPicUrl = ReadFirstValue(root["albumPic"]);

                var artists = new System.Collections.Generic.List<string>();
                JArray artistArray = root["artist"] as JArray;

                if (artistArray != null)
                {
                    foreach (JToken entry in artistArray)
                    {
                        string artist = ReadFirstValue(entry);
                        if (!string.IsNullOrEmpty(artist)) artists.Add(artist);
                    }
                }

                this.Metadata.Artist = string.Join("/", artists);
            }
            catch (Exception)
            {
                // A malformed metadata block must not prevent playback.
            }
        }

        // NetEase stores some values either as a plain string or as an array ["value", id].
        private static string ReadFirstValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            if (token.Type == JTokenType.Array)
            {
                JArray array = (JArray)token;
                return array.Count > 0 ? ReadFirstValue(array[0]) : string.Empty;
            }

            return token.ToString();
        }

        private bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data == null || data.Length < prefix.Length)
            {
                return false;
            }

            for (int i = 0; i < prefix.Length; i++)
            {
                if (data[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] AesEcbDecrypt(byte[] key, byte[] data)
        {
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }
    }
}
