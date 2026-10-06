using Dopamine.Core.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Dopamine.Core.Api.Netease
{
    /// <summary>
    /// Metadata of a song, as returned by the NetEase Cloud Music (https://music.163.com) search API.
    /// </summary>
    public class NeteaseTrackMetadata
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public IList<string> Artists { get; set; } = new List<string>();
        public string AlbumTitle { get; set; } = string.Empty;
        public IList<string> AlbumArtists { get; set; } = new List<string>();
        public int Year { get; set; }
        public int TrackNumber { get; set; }
        public int DiscNumber { get; set; }
        public string CoverUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Searches NetEase Cloud Music for a song and returns its metadata (title, artists, album,
    /// year, track number and cover art). Used to fill the song information editor.
    /// </summary>
    public class NeteaseMetadataApi
    {
        private const string apiRootUrl = "https://music.163.com/";
        private const string apiSearchUrl = "api/cloudsearch/pc";

        internal class SearchModel
        {
            public SearchResult result { get; set; }

            internal class SearchResult
            {
                public List<Song> songs { get; set; }
            }

            internal class Song
            {
                public long id { get; set; }
                public string name { get; set; }
                public List<Artist> ar { get; set; }
                public Album al { get; set; }
                public int no { get; set; }
                public string cd { get; set; }
                public long publishTime { get; set; }
            }

            internal class Artist
            {
                public string name { get; set; }
            }

            internal class Album
            {
                public string name { get; set; }
                public string picUrl { get; set; }
            }
        }

        private readonly HttpClient httpClient;

        public NeteaseMetadataApi(int timeoutSeconds)
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            this.httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(apiRootUrl)
            };

            if (timeoutSeconds > 0)
            {
                this.httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            }

            this.httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip,deflate");
            this.httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,zh-CN;q=0.8,zh;q=0.6,en;q=0.7");
            this.httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            this.httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            this.httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            this.httpClient.DefaultRequestHeaders.Add("Referer", "https://music.163.com/");
        }

        /// <summary>
        /// Searches for the given artist and title and returns the best matching song, or null when
        /// no result matches.
        /// </summary>
        public async Task<NeteaseTrackMetadata> GetMetadataAsync(string artist, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            string searchText = string.IsNullOrWhiteSpace(artist) ? title : title + " " + artist;

            var postContent = new[]
            {
                new KeyValuePair<string, string>("s", searchText),
                new KeyValuePair<string, string>("type", "1"),
                new KeyValuePair<string, string>("offset", "0"),
                new KeyValuePair<string, string>("limit", "10")
            };

            string response = await (await this.httpClient.PostAsync(apiSearchUrl, new FormUrlEncodedContent(postContent))).Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            SearchModel search = JsonConvert.DeserializeObject<SearchModel>(response);

            if (search == null || search.result == null || search.result.songs == null || search.result.songs.Count == 0)
            {
                return null;
            }

            string normalizedTitle = MatchUtils.NormalizeText(title);
            string normalizedArtist = MatchUtils.NormalizeText(artist);
            SearchModel.Song titleMatch = null;
            SearchModel.Song match = null;

            foreach (SearchModel.Song song in search.result.songs)
            {
                if (MatchUtils.NormalizeText(song.name) != normalizedTitle)
                {
                    continue;
                }

                // Prefer a result whose artist also matches.
                if (song.ar != null && normalizedArtist.Length > 0)
                {
                    foreach (SearchModel.Artist songArtist in song.ar)
                    {
                        if (MatchUtils.NormalizeText(songArtist.name) == normalizedArtist)
                        {
                            match = song;
                            break;
                        }
                    }
                }

                if (match != null)
                {
                    break;
                }

                if (titleMatch == null)
                {
                    titleMatch = song;
                }
            }

            match = match ?? titleMatch;

            if (match == null)
            {
                return null;
            }

            return this.ToMetadata(match);
        }

        private NeteaseTrackMetadata ToMetadata(SearchModel.Song song)
        {
            var metadata = new NeteaseTrackMetadata
            {
                Id = song.id,
                Title = song.name ?? string.Empty,
                AlbumTitle = song.al != null ? song.al.name ?? string.Empty : string.Empty,
                TrackNumber = song.no,
                DiscNumber = ParseDiscNumber(song.cd)
            };

            if (song.ar != null)
            {
                foreach (SearchModel.Artist artist in song.ar)
                {
                    if (!string.IsNullOrWhiteSpace(artist.name))
                    {
                        metadata.Artists.Add(artist.name);
                    }
                }
            }

            // NetEase doesn't expose a separate album artist in its search results.
            metadata.AlbumArtists = new List<string>(metadata.Artists);

            if (song.publishTime > 0)
            {
                metadata.Year = DateTimeOffset.FromUnixTimeMilliseconds(song.publishTime).Year;
            }

            if (song.al != null && !string.IsNullOrEmpty(song.al.picUrl))
            {
                // The image CDN also serves over HTTPS; prefer it.
                metadata.CoverUrl = song.al.picUrl.Replace("http://", "https://");
            }

            return metadata;
        }

        private static int ParseDiscNumber(string discNumber)
        {
            int parsed;
            return int.TryParse(discNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }
    }
}
