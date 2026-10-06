using Dopamine.Core.Api.Netease;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dopamine.Services.InfoDownload
{
    public interface IInfoDownloadService
    {
        Task<string> GetAlbumImageAsync(string albumTitle, IList<string> albumArtists, string trackTitle = "", IList<string> trackArtists = null);

        /// <summary>
        /// Searches NetEase Cloud Music for the given artist and title and returns the best
        /// matching song's metadata, or null when nothing matches.
        /// </summary>
        Task<NeteaseTrackMetadata> GetNeteaseTrackMetadataAsync(string artist, string title);
    }
}
