using Dopamine.Services.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using LyricsModel = Dopamine.Core.Api.Lyrics.Lyrics;

namespace Dopamine.Services.Lyrics
{
    public interface ILyricsService
    {
        IList<LyricsLineViewModel> ParseLyrics(LyricsModel lyrics);

        /// <summary>
        /// Gets the lyrics for the given track. When they were already fetched (for example by
        /// the background prefetch that runs when a track starts playing), they are returned
        /// straight from the cache. Set <paramref name="forceRefresh"/> to bypass the cache.
        /// </summary>
        Task<LyricsModel> GetLyricsAsync(TrackViewModel track, bool forceRefresh = false);

        /// <summary>
        /// Returns the cached lyrics for the given path, if any, without doing any I/O.
        /// </summary>
        bool TryGetLyrics(string path, out LyricsModel lyrics);

        /// <summary>
        /// Starts fetching lyrics in the background (audio tags, local .lrc file, then online)
        /// and caches the result, so they are available immediately when needed.
        /// </summary>
        void PrefetchLyrics(TrackViewModel track);

        /// <summary>
        /// Removes the cached lyrics for the given path.
        /// </summary>
        void InvalidateLyrics(string path);
    }
}
