using Digimezzo.Foundation.Core.Logging;
using Dopamine.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dopamine.Core.Api.Lyrics
{
    public class LyricsFactory
    {
        private readonly IList<ILyricsApi> lyricsApis;
        private readonly Random random = new Random();

        public LyricsFactory(int timeoutSeconds, string providers, ILocalizationInfo info)
        {
            lyricsApis = new List<ILyricsApi>();

            if (providers.ToLower().Contains("chartlyrics")) lyricsApis.Add(new ChartLyricsApi(timeoutSeconds));
            if (providers.ToLower().Contains("lololyrics")) lyricsApis.Add(new LololyricsApi(timeoutSeconds));
            if (providers.ToLower().Contains("metrolyrics")) lyricsApis.Add(new MetroLyricsApi(timeoutSeconds));
            if (providers.ToLower().Contains("xiamilyrics")) lyricsApis.Add(new XiamiLyricsApi(timeoutSeconds, info));
            if (providers.ToLower().Contains("neteaselyrics")) lyricsApis.Add(new NeteaseLyricsApi(timeoutSeconds, info));
        }

        public async Task<Lyrics> GetLyricsAsync(string artist, string title)
        {
            Lyrics lyrics = null;

            // A fresh pipe per call: the APIs are tried in a random order, each one only
            // once. Using a local pipe (instead of shared state) also makes concurrent
            // calls (e.g. background prefetch while the lyrics page is opening) safe.
            var pipe = new List<ILyricsApi>(this.lyricsApis);

            var api = this.GetRandomApi(pipe);

            while (api != null && (lyrics == null || !lyrics.HasText))
            {
                try
                {
                    lyrics = new Lyrics(await api.GetLyricsAsync(artist, title), api.SourceName);
                }
                catch (Exception ex)
                {
                    LogClient.Error("Error while getting lyrics from '{0}'. Exception: {1}", api.SourceName, ex.Message);
                }

                api = this.GetRandomApi(pipe);
            }

            return lyrics;
        }

        private ILyricsApi GetRandomApi(IList<ILyricsApi> pipe)
        {
            ILyricsApi api = null;

            if (pipe.Count > 0)
            {
                int index;

                lock (this.random)
                {
                    index = this.random.Next(pipe.Count);
                }

                api = pipe[index];
                pipe.RemoveAt(index);
            }

            return api;
        }
    }
}
