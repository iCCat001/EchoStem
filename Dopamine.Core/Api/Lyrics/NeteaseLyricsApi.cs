using Digimezzo.Foundation.Core.Settings;
using Dopamine.Core.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Dopamine.Core.Api.Lyrics
{
    // Public NetEase Cloud Music web API (https://music.163.com).
    public class NeteaseLyricsApi : ILyricsApi
    {
        private const string apiRootUrl = "https://music.163.com/";
        private const string apiSearchUrl = "api/cloudsearch/pc";
        private const string apiLyricsFormat = "api/song/lyric?id={0}&lv=-1&kv=-1&tv=-1";

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
            }
        }

        internal class LyricModel
        {
            public Lrc lrc { get; set; }
            public Lrc tlyric { get; set; }

            internal class Lrc
            {
                public int version { get; set; }
                public string lyric { get; set; }
            }
        }

        private readonly ILocalizationInfo info;
        private readonly HttpClient httpClient;
        private readonly bool enableTLyric;

        public NeteaseLyricsApi(int timeoutSeconds, ILocalizationInfo info)
        {
            this.info = info;
            this.enableTLyric = SettingsClient.Get<string>("Appearance", "Language") == "ZH-CN";

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

        private async Task<long?> ParseTrackIdAsync(string artist, string title)
        {
            var postContent = new[]
            {
                new KeyValuePair<string, string>("s", title + " " + artist),
                new KeyValuePair<string, string>("type", "1"),
                new KeyValuePair<string, string>("offset", "0"),
                new KeyValuePair<string, string>("limit", "1")
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

            return search.result.songs[0].id;
        }

        private async Task<string> ParseLyricsAsync(long trackId)
        {
            string response = await this.httpClient.GetStringAsync(string.Format(apiLyricsFormat, trackId));

            if (string.IsNullOrWhiteSpace(response))
            {
                return string.Empty;
            }

            LyricModel lyrics = JsonConvert.DeserializeObject<LyricModel>(response);

            if (lyrics == null || lyrics.lrc == null || string.IsNullOrEmpty(lyrics.lrc.lyric))
            {
                return string.Empty;
            }

            if (!this.enableTLyric || lyrics.tlyric == null || string.IsNullOrEmpty(lyrics.tlyric.lyric))
            {
                return lyrics.lrc.lyric;
            }

            return MergeTranslation(lyrics.lrc.lyric, lyrics.tlyric.lyric);
        }

        private static string MergeTranslation(string original, string translation)
        {
            Dictionary<string, string> originalLines = ParseTimestampedLyrics(original);
            Dictionary<string, string> translatedLines = ParseTimestampedLyrics(translation);

            var output = new StringBuilder();

            foreach (KeyValuePair<string, string> line in originalLines)
            {
                output.Append(line.Key).Append(']').Append(line.Value);

                if (translatedLines.ContainsKey(line.Key))
                {
                    output.Append("%%Trans%%").Append(translatedLines[line.Key]);
                }

                output.Append('\n');
            }

            return output.ToString();
        }

        private static Dictionary<string, string> ParseTimestampedLyrics(string lyrics)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(lyrics))
            {
                return result;
            }

            foreach (string line in lyrics.Split('\n'))
            {
                int bracketIndex = line.IndexOf(']');

                if (bracketIndex <= 0)
                {
                    continue;
                }

                string timestamp = line.Substring(0, bracketIndex);
                string content = line.Substring(bracketIndex + 1);

                if (!result.ContainsKey(timestamp))
                {
                    result[timestamp] = content;
                }
            }

            return result;
        }

        public string SourceName => this.info.NeteaseLyrics;

        public async Task<string> GetLyricsAsync(string artist, string title)
        {
            long? trackId = await this.ParseTrackIdAsync(artist, title);

            if (trackId == null)
            {
                return string.Empty;
            }

            return await this.ParseLyricsAsync(trackId.Value);
        }
    }
}
