using Digimezzo.Foundation.Core.Settings;
using Dopamine.Core.Api.Lastfm;
using Dopamine.Core.Api.Netease;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dopamine.Services.InfoDownload
{
    public class InfoDownloadService : IInfoDownloadService
    {
        public async Task<string> GetAlbumImageAsync(string albumTitle, IList<string> albumArtists, string trackTitle = "", IList<string> trackArtists = null)
        {
            // NetEase searches songs, so prefer the track title there; Last.fm searches albums, so
            // prefer the album title there.
            string songSearchTitle = !string.IsNullOrEmpty(trackTitle) ? trackTitle : albumTitle;
            string albumSearchTitle = !string.IsNullOrEmpty(albumTitle) ? albumTitle : trackTitle;

            // Artist: prefer the track artists, then the album artists.
            List<string> artists = new List<string>();

            if (trackArtists != null && trackArtists.Count > 0)
            {
                artists.AddRange(trackArtists.Where(a => !string.IsNullOrEmpty(a)));
            }

            if (albumArtists != null && albumArtists.Count > 0)
            {
                artists.AddRange(albumArtists.Where(a => !string.IsNullOrEmpty(a)));
            }

            if (string.IsNullOrEmpty(songSearchTitle))
            {
                return null;
            }

            // 1) NetEase Cloud Music first (same source as the song editor's "get cover").
            List<string> neteaseArtists = artists.Count > 0 ? artists : new List<string> { string.Empty };

            foreach (string artist in neteaseArtists)
            {
                try
                {
                    NeteaseTrackMetadata metadata = await this.GetNeteaseTrackMetadataAsync(artist, songSearchTitle);

                    if (metadata != null && !string.IsNullOrEmpty(metadata.CoverUrl))
                    {
                        return metadata.CoverUrl;
                    }
                }
                catch (Exception)
                {
                    // Ignore and fall back to Last.fm.
                }
            }

            // 2) Fall back to Last.fm.
            if (string.IsNullOrEmpty(albumSearchTitle))
            {
                return null;
            }

            foreach (string artist in artists)
            {
                LastFmAlbum lfmAlbum = await LastfmApi.AlbumGetInfo(artist, albumSearchTitle, false, "EN");

                if (!string.IsNullOrEmpty(lfmAlbum.LargestImage()))
                {
                    return lfmAlbum.LargestImage();
                }
            }

            return null;
        }

        public async Task<NeteaseTrackMetadata> GetNeteaseTrackMetadataAsync(string artist, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            int timeoutSeconds = SettingsClient.Get<int>("Lyrics", "TimeoutSeconds");
            var api = new NeteaseMetadataApi(timeoutSeconds > 0 ? timeoutSeconds : 10);

            return await api.GetMetadataAsync(artist, title);
        }
    }
}
