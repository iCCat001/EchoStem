using System.Collections.Generic;

namespace Dopamine.Core.Audio
{
    /// <summary>
    /// Metadata stored inside a NetEase Cloud Music (NCM) container.
    /// </summary>
    public class NcmMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public int Bitrate { get; set; }
        public int Duration { get; set; }
        public byte[] Cover { get; set; }
        public string CoverMimeType { get; set; } = string.Empty;

        /// <summary>
        /// URL of the album cover on the NetEase CDN, taken from the NCM metadata. Recent versions
        /// of NetEase Cloud Music no longer embed the cover in the NCM file, but store this URL
        /// instead; it is used to fetch the cover when converting.
        /// </summary>
        public string AlbumPicUrl { get; set; } = string.Empty;
    }
}
