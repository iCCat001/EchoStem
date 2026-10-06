using System.IO;

namespace Dopamine.Core.Audio
{
    /// <summary>
    /// Exposes NetEase Cloud Music (NCM) files as a normal audio stream.
    /// </summary>
    public class NcmContainer : IAudioContainer
    {
        public bool IsContainer(string filename)
        {
            return NcmFile.IsNcmFile(filename);
        }

        public Stream OpenPayload(string filename, out string payloadExtension)
        {
            var ncmFile = new NcmFile(filename);
            payloadExtension = ncmFile.PayloadExtension;
            return ncmFile.OpenPayload();
        }
    }
}
