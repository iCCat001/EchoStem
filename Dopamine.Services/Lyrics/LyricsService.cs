using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using Dopamine.Core.Api.Lyrics;
using Dopamine.Core.Base;
using Dopamine.Core.Helpers;
using Dopamine.Core.Utils;
using Dopamine.Data.Metadata;
using Dopamine.Services.Entities;
using Dopamine.Services.I18n;
using Dopamine.Services.Metadata;
using Dopamine.Services.Playback;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LyricsModel = Dopamine.Core.Api.Lyrics.Lyrics;

namespace Dopamine.Services.Lyrics
{
    public class LyricsService : ILyricsService
    {
        // Upper bound for the lyrics cache, to avoid unbounded growth during long sessions.
        private const int MaxCachedLyrics = 100;

        // The internal marker which separates a lyric line from its translation. Local .lrc files
        // store both lines separately (bilingual .lrc format) instead of this marker.
        private const string TranslationMarker = "%%Trans%%";

        // Legacy: an invisible separator which used to be written to .lrc files. Kept so files
        // created by a previous version still read correctly.
        private const string LegacyTranslationSeparator = "\u2063"; // INVISIBLE SEPARATOR

        // NetEase returns this placeholder instead of real lyrics for instrumental tracks. It is
        // still shown as the lyrics, but must not be written to a local .lrc file.
        private const string InstrumentalPlaceholder = "纯音乐，请欣赏";
        private const string InstrumentalPlaceholderAlternative = "纯音乐,请欣赏";

        private static readonly Regex LyricTimestampRegex = new Regex(@"\[\d+:\d+(?:[.:]\d+)?\]", RegexOptions.Compiled);

        private readonly IMetadataService metadataService;
        private readonly IPlaybackService playbackService;
        private readonly II18nService i18nService;
        private readonly ILocalizationInfo localizationInfo;

        private readonly ConcurrentDictionary<string, LyricsModel> lyricsCache = new ConcurrentDictionary<string, LyricsModel>();
        private readonly ConcurrentDictionary<string, Lazy<Task<LyricsModel>>> inFlightLyrics = new ConcurrentDictionary<string, Lazy<Task<LyricsModel>>>();

        // Allows a single background prefetch worker which always processes the most recently
        // requested track (intermediate tracks are skipped when skipping fast).
        private readonly object prefetchLock = new object();
        private TrackViewModel pendingPrefetchTrack;
        private bool isPrefetching;

        private volatile LyricsFactory lyricsFactory;

        public LyricsService(IMetadataService metadataService, IPlaybackService playbackService, II18nService i18nService, ILocalizationInfo localizationInfo)
        {
            this.metadataService = metadataService;
            this.playbackService = playbackService;
            this.i18nService = i18nService;
            this.localizationInfo = localizationInfo;

            this.lyricsFactory = this.CreateLyricsFactory();

            // The lyrics of the previous tracks become stale when their metadata is edited.
            this.metadataService.MetadataChanged += (_) => this.lyricsCache.Clear();

            // Netease can return translated lyrics depending on the UI language.
            this.i18nService.LanguageChanged += (_, __) =>
            {
                this.lyricsCache.Clear();
                this.lyricsFactory = this.CreateLyricsFactory();
            };

            // Providers or timeout can be changed in the settings.
            SettingsClient.SettingChanged += (_, e) =>
            {
                if (SettingsClient.IsSettingChanged(e, "Lyrics", "Providers") || SettingsClient.IsSettingChanged(e, "Lyrics", "TimeoutSeconds"))
                {
                    this.lyricsFactory = this.CreateLyricsFactory();
                }
            };

            // Prefetch the lyrics as soon as a track starts playing.
            this.playbackService.PlaybackSuccess += (_, __) => this.PrefetchLyrics(this.playbackService.CurrentTrack);
        }

        private LyricsFactory CreateLyricsFactory()
        {
            return new LyricsFactory(
                SettingsClient.Get<int>("Lyrics", "TimeoutSeconds"),
                SettingsClient.Get<string>("Lyrics", "Providers"),
                this.localizationInfo);
        }

        public bool TryGetLyrics(string path, out LyricsModel lyrics)
        {
            lyrics = null;

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return this.lyricsCache.TryGetValue(path, out lyrics);
        }

        public void InvalidateLyrics(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            this.lyricsCache.TryRemove(path, out _);
        }

        public Task<LyricsModel> GetLyricsAsync(TrackViewModel track, bool forceRefresh = false)
        {
            if (track == null)
            {
                return Task.FromResult(new LyricsModel());
            }

            string path = track.Path;

            if (!forceRefresh)
            {
                if (this.lyricsCache.TryGetValue(path, out LyricsModel cached))
                {
                    return Task.FromResult(cached);
                }

                // Share a single fetch when several callers (background prefetch and the
                // lyrics page) ask for the same track at the same time.
                Lazy<Task<LyricsModel>> lazy = this.inFlightLyrics.GetOrAdd(
                    path,
                    _ => new Lazy<Task<LyricsModel>>(() => this.AcquireAndCacheAsync(track)));

                Task<LyricsModel> task = lazy.Value;
                task.ContinueWith(t => this.inFlightLyrics.TryRemove(path, out _), TaskScheduler.Default);

                return task;
            }

            return this.AcquireAndCacheAsync(track);
        }

        public void PrefetchLyrics(TrackViewModel track)
        {
            if (track == null)
            {
                return;
            }

            lock (this.prefetchLock)
            {
                this.pendingPrefetchTrack = track;

                if (this.isPrefetching)
                {
                    return;
                }

                this.isPrefetching = true;
            }

            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        TrackViewModel toFetch;

                        lock (this.prefetchLock)
                        {
                            toFetch = this.pendingPrefetchTrack;
                            this.pendingPrefetchTrack = null;

                            if (toFetch == null)
                            {
                                this.isPrefetching = false;
                                return;
                            }
                        }

                        try
                        {
                            await this.GetLyricsAsync(toFetch);
                        }
                        catch (Exception ex)
                        {
                            LogClient.Error("Could not prefetch lyrics for Track {0}. Exception: {1}", toFetch.Path, ex.Message);
                        }
                    }
                }
                catch
                {
                    lock (this.prefetchLock)
                    {
                        this.isPrefetching = false;
                    }
                }
            });
        }

        public async Task<LyricsModel> GetLyricsFromOnlineAsync(TrackViewModel track)
        {
            if (track == null)
            {
                return new LyricsModel();
            }

            FileMetadata fmd = await this.metadataService.GetFileMetadataAsync(track.Path);

            if (fmd == null)
            {
                return new LyricsModel();
            }

            string artist = fmd.Artists != null && fmd.Artists.Values != null && fmd.Artists.Values.Length > 0 ? fmd.Artists.Values[0] : string.Empty;
            string title = fmd.Title != null && fmd.Title.Value != null ? fmd.Title.Value : string.Empty;

            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
            {
                return new LyricsModel();
            }

            LyricsModel onlineLyrics = null;

            try
            {
                onlineLyrics = await this.lyricsFactory.GetLyricsAsync(artist, title);
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not get lyrics online for Track {0}. Exception: {1}", track.Path, ex.Message);
            }

            if (onlineLyrics == null || !onlineLyrics.HasText)
            {
                return new LyricsModel();
            }

            onlineLyrics.SourceType = SourceTypeEnum.Online;

            // Force the local .lrc file to be replaced. When it does not exist yet, only create
            // it when saving online lyrics locally is enabled.
            string lrcFile = Path.Combine(Path.GetDirectoryName(fmd.Path), Path.GetFileNameWithoutExtension(fmd.Path) + FileFormats.LRC);

            if (System.IO.File.Exists(lrcFile) || SettingsClient.Get<bool>("Lyrics", "SaveOnlineLyricsToLocal"))
            {
                if (!IsInstrumentalPlaceholder(onlineLyrics.Text))
                {
                    await this.SaveLyricsToLrcFileAsync(lrcFile, track.Path, onlineLyrics.Text);
                }
            }

            // Update the cache so the freshly fetched lyrics are used immediately.
            if (this.lyricsCache.Count >= MaxCachedLyrics)
            {
                this.lyricsCache.Clear();
            }

            this.lyricsCache[track.Path] = onlineLyrics;

            return onlineLyrics;
        }

        private async Task<LyricsModel> AcquireAndCacheAsync(TrackViewModel track)
        {
            LyricsModel lyrics = await this.AcquireLyricsAsync(track);

            if (this.lyricsCache.Count >= MaxCachedLyrics)
            {
                this.lyricsCache.Clear();
            }

            this.lyricsCache[track.Path] = lyrics;

            return lyrics;
        }

        private async Task<LyricsModel> AcquireLyricsAsync(TrackViewModel track)
        {
            var lyrics = new LyricsModel(string.Empty, string.Empty, SourceTypeEnum.Audio);

            FileMetadata fmd = await this.metadataService.GetFileMetadataAsync(track.Path);

            if (fmd == null)
            {
                return lyrics;
            }

            string lrcFile = Path.Combine(Path.GetDirectoryName(fmd.Path), Path.GetFileNameWithoutExtension(fmd.Path) + FileFormats.LRC);

            // 1. A local .lrc file with the same name next to the audio file. This has the
            //    highest priority: when it exists, it is used as the lyrics.
            if (System.IO.File.Exists(lrcFile))
            {
                try
                {
                    using (var fs = new FileStream(lrcFile, FileMode.Open, FileAccess.Read))
                    {
                        // UTF-8 (with BOM) is detected automatically; BOM-less files keep using
                        // the system default encoding.
                        using (var sr = new StreamReader(fs, Encoding.Default, true))
                        {
                            string rawText = await sr.ReadToEndAsync();
                            var lrcLyrics = new LyricsModel(MergeTranslations(rawText), string.Empty, SourceTypeEnum.Lrc);

                            if (lrcLyrics.HasText)
                            {
                                lyrics = lrcLyrics;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogClient.Error("Could not read the local lyrics file for Track {0}. Exception: {1}", track.Path, ex.Message);
                }
            }

            // 2. Lyrics stored in the audio file tags.
            if (!lyrics.HasText)
            {
                lyrics = new LyricsModel(
                    fmd.Lyrics != null && fmd.Lyrics.Value != null ? fmd.Lyrics.Value : string.Empty,
                    string.Empty,
                    SourceTypeEnum.Audio);
            }

            // 3. Online lyrics, when automatic download is enabled.
            if (!lyrics.HasText && SettingsClient.Get<bool>("Lyrics", "DownloadLyrics"))
            {
                string artist = fmd.Artists != null && fmd.Artists.Values != null && fmd.Artists.Values.Length > 0 ? fmd.Artists.Values[0] : string.Empty;
                string title = fmd.Title != null && fmd.Title.Value != null ? fmd.Title.Value : string.Empty;

                if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
                {
                    try
                    {
                        LyricsModel onlineLyrics = await this.lyricsFactory.GetLyricsAsync(artist, title);

                        if (onlineLyrics != null && onlineLyrics.HasText)
                        {
                            onlineLyrics.SourceType = SourceTypeEnum.Online;
                            lyrics = onlineLyrics;

                            // Optionally store the downloaded lyrics as a local .lrc file, so that
                            // they are used directly (and shown as "Local lyrics") next time.
                            if (SettingsClient.Get<bool>("Lyrics", "SaveOnlineLyricsToLocal") && !IsInstrumentalPlaceholder(onlineLyrics.Text))
                            {
                                await this.SaveLyricsToLrcFileAsync(lrcFile, track.Path, onlineLyrics.Text);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogClient.Error("Could not get lyrics online for Track {0}. Exception: {1}", track.Path, ex.Message);
                    }
                }
            }

            return lyrics;
        }

        // Detects the NetEase "纯音乐，请欣赏" placeholder, which means the track has no lyrics.
        // Such lyrics are still displayed, but never written to a local .lrc file.
        private static bool IsInstrumentalPlaceholder(string lyricsText)
        {
            if (string.IsNullOrWhiteSpace(lyricsText))
            {
                return false;
            }

            string stripped = LyricTimestampRegex.Replace(lyricsText, string.Empty);
            stripped = Regex.Replace(stripped, @"\s", string.Empty);

            return stripped == InstrumentalPlaceholder || stripped == InstrumentalPlaceholderAlternative;
        }

        private async Task SaveLyricsToLrcFileAsync(string lrcFile, string trackPath, string lyricsText)
        {
            try
            {
                using (var fs = new FileStream(lrcFile, FileMode.Create, FileAccess.Write))
                {
                    using (var sw = new StreamWriter(fs, new UTF8Encoding(true)))
                    {
                        await sw.WriteAsync(SplitTranslations(lyricsText));
                    }
                }

                LogClient.Info("Saved the online lyrics to the local file {0} for Track {1}", lrcFile, trackPath);
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not save the online lyrics to the local file {0} for Track {1}. Exception: {2}", lrcFile, trackPath, ex.Message);
            }
        }

        // Splits lines like "[00:21.062]original%%Trans%%translation" into two bilingual .lrc
        // lines sharing the same timestamp. The .lrc file stays human readable, without any
        // special separator character.
        private static string SplitTranslations(string lyricsText)
        {
            string normalized = lyricsText.Replace("\r\n", "\n").Replace('\r', '\n');
            var output = new List<string>();

            foreach (string line in normalized.Split('\n'))
            {
                int markerIndex = line.IndexOf(TranslationMarker, StringComparison.Ordinal);

                if (markerIndex < 0)
                {
                    output.Add(line);
                    continue;
                }

                string original = line.Substring(0, markerIndex);
                string translation = line.Substring(markerIndex + TranslationMarker.Length);
                string timestamp = GetLeadingTimestamp(line);

                if (timestamp != null)
                {
                    output.Add(original);
                    output.Add(timestamp + translation);
                }
                else
                {
                    output.Add(original + " " + translation);
                }
            }

            return string.Join("\n", output);
        }

        // Merges consecutive .lrc lines sharing the same timestamp back into the internal
        // "original%%Trans%%translation" form.
        private static string MergeTranslations(string rawText)
        {
            string normalized = rawText
                .Replace(LegacyTranslationSeparator, TranslationMarker)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            var output = new List<string>();

            int i = 0;
            while (i < lines.Length)
            {
                string timestamp = GetLeadingTimestamp(lines[i]);

                if (timestamp != null && i + 1 < lines.Length && GetLeadingTimestamp(lines[i + 1]) == timestamp)
                {
                    output.Add(lines[i] + TranslationMarker + lines[i + 1].Substring(timestamp.Length));
                    i += 2;
                }
                else
                {
                    output.Add(lines[i]);
                    i++;
                }
            }

            return string.Join("\n", output);
        }

        // Returns the leading run of time like timestamps (e.g. "[00:21.062]" or
        // "[00:01][00:02]"), or null when the line does not start with one (e.g. [ar:...]).
        private static string GetLeadingTimestamp(string line)
        {
            if (string.IsNullOrEmpty(line) || line[0] != '[')
            {
                return null;
            }

            int index = 0;

            while (index < line.Length && line[index] == '[')
            {
                int close = line.IndexOf(']', index);

                if (close < 0)
                {
                    return null;
                }

                string inner = line.Substring(index + 1, close - index - 1);

                if (!Regex.IsMatch(inner, @"^\d+:\d+(\.\d+)?$"))
                {
                    return null;
                }

                index = close + 1;
            }

            return index > 0 ? line.Substring(0, index) : null;
        }

        private int GetNumberOfFollowingEmptyLines(ref PeekStringReader reader)
        {
            int numberOfEmptyLines = 0;

            string peekedLine = reader.PeekLine();

            while (peekedLine != null && peekedLine.Length == 0)
            {
                // The next line is an empty line
                numberOfEmptyLines++;
                reader.ReadLine();
                peekedLine = reader.PeekLine();
            }

            return numberOfEmptyLines;
        }

        private void AddEmptyLines(int numberOfEmptyLines, List<LyricsLineViewModel> lines, TimeSpan span)
        {
            for (int i = 0; i < numberOfEmptyLines; i++)
            {
                if (span.Equals(TimeSpan.MinValue))
                {
                    lines.Add(new LyricsLineViewModel(string.Empty));
                }
                else
                {
                    lines.Add(new LyricsLineViewModel(span, string.Empty));
                }  
            }
        }

        public IList<LyricsLineViewModel> ParseLyrics(LyricsModel lyrics)
        {
            var linesWithTimestamps = new List<LyricsLineViewModel>();
            var linesWithoutTimestamps = new List<LyricsLineViewModel>();

            var reader = new PeekStringReader(lyrics.Text);

            string line;

            while (true)
            {
                // Process 1 line
                line = reader.ReadLine();

                if (line == null)
                {
                    // No line found, we reached the end. Exit while loop.
                    break;
                }

                // Ignore empty lines
                if (line.Length == 0)
                {
                    // Process the next line.
                    continue;
                }

                // Ignore lines with tags
                MatchCollection tagMatches = Regex.Matches(line, @"\[[a-z]+?:.*?\]");

                if (tagMatches.Count > 0)
                {
                    // This is a tag: ignore this line and process the next line.
                    continue;
                }

                //C-Cat: 换行显示译文
                try
                {
                    line = line.Replace("%%Trans%%", "\n");
                }
                catch (Exception ex) { }

                // Check if the line has characters and is enclosed in brackets (starts with [ and ends with ]).
                if (!(line.StartsWith("[") && line.LastIndexOf(']') > 0))
                {
                    // This line is not enclosed in brackets, so it cannot have timestamps.
                    linesWithoutTimestamps.Add(new LyricsLineViewModel(line));
                    int numberOfEmptyLines = this.GetNumberOfFollowingEmptyLines(ref reader);

                    // Add empty lines
                    this.AddEmptyLines(numberOfEmptyLines, linesWithoutTimestamps, TimeSpan.MinValue);

                    // Process the next line
                    continue;
                }

                // Get all substrings between square brackets for this line
                MatchCollection ms = Regex.Matches(line, @"\[.*?\]");
                var spans = new List<TimeSpan>();
                bool couldParseAllTimestamps = true;

                // Loop through all matches
                foreach (Match m in ms)
                {
                    var time = TimeSpan.Zero;
                    string subString = m.Value.Trim('[', ']');

                    if (FormatUtils.ParseLyricsTime(subString, out time))
                    {
                        spans.Add(time);
                    }
                    else
                    {
                        couldParseAllTimestamps = false;
                    }
                }

                // Check if all timestamps could be parsed
                if (couldParseAllTimestamps)
                {
                    int startIndex = line.LastIndexOf(']') + 1;
                    int numberOfEmptyLines = this.GetNumberOfFollowingEmptyLines(ref reader);

                    foreach (TimeSpan span in spans)
                    {
                        linesWithTimestamps.Add(new LyricsLineViewModel(span, line.Substring(startIndex)));

                        // Add empty lines
                        this.AddEmptyLines(numberOfEmptyLines, linesWithTimestamps, span);
                    }
                }
                else
                {
                    // The line has mistakes. Consider it as a line without timestamps.
                    linesWithoutTimestamps.Add(new LyricsLineViewModel(line));
                    int numberOfEmptyLines = this.GetNumberOfFollowingEmptyLines(ref reader);

                    // Add empty lines
                    this.AddEmptyLines(numberOfEmptyLines, linesWithoutTimestamps, TimeSpan.MinValue);
                }
            }

            // Order the time stamped lines
            linesWithTimestamps = new List<LyricsLineViewModel>(linesWithTimestamps.OrderBy(p => p.Time));

            // Merge both collections, lines with timestamps first.
            linesWithTimestamps.AddRange(linesWithoutTimestamps);

            return linesWithTimestamps;
        }
    }
}
