using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Core.Base;
using Dopamine.Services.Entities;
using Dopamine.Services.Lyrics;
using Dopamine.Services.Metadata;
using Dopamine.Services.Playback;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Dopamine.Services.Sync
{
    /// <summary>
    /// A deliberately small HTTP bridge on 127.0.0.1. It is not a web app: the
    /// DSH plugin polls JSON, grabs the album art, and posts simple commands.
    ///
    /// Using a raw <see cref="TcpListener"/> (instead of HttpListener) avoids the
    /// Windows URL reservation requirement, so a normal user can enable the
    /// setting without an elevated shell.
    /// </summary>
    public sealed class DshSyncService : IDshSyncService, IDisposable
    {
        public const string SettingsSection = "Integration";
        public const string EnabledSettingKey = "DshSyncEnabled";
        public const string PortSettingKey = "DshSyncPort";
        public const int DefaultPort = 38721;

        private const int MaxHeaderBytes = 32 * 1024;
        private const int MaxBodyBytes = 64 * 1024;
        private const int ClientTimeoutMilliseconds = 5000;

        private readonly IPlaybackService playbackService;
        private readonly ILyricsService lyricsService;
        private readonly IMetadataService metadataService;

        private readonly object lifecycleLock = new object();
        private readonly object assetLock = new object();

        private TcpListener listener;
        private CancellationTokenSource cancellation;
        private Task acceptTask;
        private int listeningPort;

        private int trackGeneration;

        private byte[] coverBytes;
        private string coverMime;
        private string coverVersion;

        private IList<LyricLineDto> lyricLines;
        private string lyricsVersion;

        private bool disposed;

        public DshSyncService(IPlaybackService playbackService, ILyricsService lyricsService, IMetadataService metadataService)
        {
            this.playbackService = playbackService;
            this.lyricsService = lyricsService;
            this.metadataService = metadataService;

            SettingsClient.SettingChanged += this.SettingsClient_SettingChanged;
            this.playbackService.PlayingTrackChanged += this.PlaybackService_PlayingTrackChanged;
            this.playbackService.PlaybackSuccess += this.PlaybackService_PlaybackSuccess;

            if (SettingsClient.Get<bool>(SettingsSection, EnabledSettingKey))
            {
                this.ScheduleStart();
            }
        }

        /// <summary>
        /// Binds the listener only after the shell has finished its startup work.
        /// Doing this from a container callback can run before the first window is
        /// shown; deferring keeps socket setup off that critical path.
        /// </summary>
        private void ScheduleStart()
        {
            Application app = Application.Current;

            if (app == null)
            {
                this.Start();
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!this.disposed)
                {
                    this.Start();
                }
            }), DispatcherPriority.ApplicationIdle);
        }

        public bool IsRunning
        {
            get
            {
                lock (this.lifecycleLock)
                {
                    return this.listener != null;
                }
            }
        }

        public int Port
        {
            get
            {
                lock (this.lifecycleLock)
                {
                    return this.listeningPort > 0 ? this.listeningPort : this.GetConfiguredPort();
                }
            }
        }

        private int GetConfiguredPort()
        {
            int configured = SettingsClient.Get<int>(SettingsSection, PortSettingKey);
            return configured > 0 && configured <= 65535 ? configured : DefaultPort;
        }

        public void Start()
        {
            lock (this.lifecycleLock)
            {
                if (this.disposed || this.listener != null)
                {
                    return;
                }

                int port = this.GetConfiguredPort();
                TcpListener candidate = null;

                try
                {
                    candidate = new TcpListener(IPAddress.Loopback, port);
                    candidate.Start();

                    var cts = new CancellationTokenSource();
                    this.listener = candidate;
                    this.cancellation = cts;
                    this.listeningPort = ((IPEndPoint)candidate.LocalEndpoint).Port;
                    this.acceptTask = Task.Run(() => this.AcceptLoopAsync(candidate, cts.Token));

                    LogClient.Info("DSH sync bridge listening on 127.0.0.1:{0}", this.listeningPort);
                }
                catch (Exception ex)
                {
                    if (candidate != null)
                    {
                        try { candidate.Stop(); } catch { /* ignore */ }
                    }

                    this.listeningPort = 0;
                    LogClient.Error("Could not start the DSH sync bridge on port {0}. Exception: {1}", port, ex.Message);
                }
            }
        }

        public void Stop()
        {
            TcpListener oldListener;
            CancellationTokenSource oldCancellation;
            Task oldAcceptTask;

            lock (this.lifecycleLock)
            {
                oldListener = this.listener;
                oldCancellation = this.cancellation;
                oldAcceptTask = this.acceptTask;

                this.listener = null;
                this.cancellation = null;
                this.acceptTask = null;
                this.listeningPort = 0;
            }

            if (oldCancellation != null)
            {
                try { oldCancellation.Cancel(); } catch { /* ignore */ }
            }

            if (oldListener != null)
            {
                try { oldListener.Stop(); } catch { /* ignore */ }
                LogClient.Info("DSH sync bridge stopped");
            }

            // The accept loop only waits on the listener, so it completes as soon
            // as Stop() returns. Do not block the UI thread waiting for it.
            if (oldAcceptTask != null)
            {
                oldAcceptTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }

            if (oldCancellation != null)
            {
                oldCancellation.Dispose();
            }
        }

        private async Task AcceptLoopAsync(TcpListener activeListener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await activeListener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogClient.Error("DSH sync bridge accept failed. Exception: {0}", ex.Message);
                    break;
                }

                if (token.IsCancellationRequested)
                {
                    try { client.Close(); } catch { /* ignore */ }
                    break;
                }

                // One short-lived task per request. The bridge is polled at most a
                // few times per second by one local page, so this stays tiny.
                _ = Task.Run(() => this.HandleClientAsync(client, token));
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = ClientTimeoutMilliseconds;
                    client.SendTimeout = ClientTimeoutMilliseconds;

                    NetworkStream stream = client.GetStream();
                    HttpRequest request = ReadRequest(stream);

                    if (request == null)
                    {
                        return;
                    }

                    await this.ProcessRequestAsync(stream, request).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("DSH sync bridge request failed. Exception: {0}", ex.Message);
            }
        }

        private async Task ProcessRequestAsync(NetworkStream stream, HttpRequest request)
        {
            string method = request.Method;
            string path = request.Path;

            if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                WriteResponse(stream, 204, "No Content", "text/plain; charset=utf-8", new byte[0]);
                return;
            }

            if (string.Equals(path, "/", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(stream, 200, JsonConvert.SerializeObject(new
                {
                    ok = true,
                    app = "EchoStem",
                    message = "EchoStem DSH sync bridge",
                    endpoints = new[] { "/api/ping", "/api/state", "/api/lyrics", "/api/cover", "/api/command" }
                }));
                return;
            }

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                switch (path.ToLowerInvariant())
                {
                    case "/api/ping":
                        WriteJson(stream, 200, JsonConvert.SerializeObject(new
                        {
                            ok = true,
                            app = "EchoStem",
                            port = this.Port,
                            serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        }));
                        return;

                    case "/api/state":
                        WriteJson(stream, 200, this.BuildStateJson());
                        return;

                    case "/api/lyrics":
                        WriteJson(stream, 200, this.BuildLyricsJson());
                        return;

                    case "/api/cover":
                        this.WriteCover(stream);
                        return;
                }
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/api/command", StringComparison.OrdinalIgnoreCase))
            {
                string result = await this.ExecuteCommandAsync(request.BodyText).ConfigureAwait(false);
                WriteJson(stream, 200, result);
                return;
            }

            WriteJson(stream, 404, "{\"ok\":false,\"error\":\"not-found\"}");
        }

        private string BuildStateJson()
        {
            string stateJson;

            try
            {
                stateJson = OnUi(() =>
                {
                    TrackViewModel track = this.playbackService.CurrentTrack;
                    bool hasTrack = track != null;

                    double positionMs = 0;
                    double durationMs = 0;

                    if (hasTrack)
                    {
                        try { positionMs = this.playbackService.GetCurrentTime.TotalMilliseconds; } catch { positionMs = 0; }
                        try { durationMs = this.playbackService.GetTotalTime.TotalMilliseconds; } catch { durationMs = 0; }

                        if (durationMs <= 0 && track.Track.Duration.HasValue)
                        {
                            durationMs = track.Track.Duration.Value;
                        }
                    }

                    if (durationMs <= 0)
                    {
                        durationMs = 0;
                    }

                    if (positionMs < 0)
                    {
                        positionMs = 0;
                    }

                    if (durationMs > 0 && positionMs > durationMs)
                    {
                        positionMs = durationMs;
                    }

                    bool playing = hasTrack && this.playbackService.IsPlaying;
                    bool stopped = !hasTrack || this.playbackService.IsStopped;
                    string status = !hasTrack ? "idle" : playing ? "playing" : stopped ? "stopped" : "paused";

                    string trackId = hasTrack ? HashPath(track.Path) : null;
                    string coverVersion;
                    string lyricsVersion;
                    int lyricsCount;

                    lock (this.assetLock)
                    {
                        coverVersion = this.coverVersion;
                        lyricsVersion = this.lyricsVersion;
                        lyricsCount = this.lyricLines == null ? 0 : this.lyricLines.Count;
                    }

                    var payload = new
                    {
                        ok = true,
                        app = "EchoStem",
                        status,
                        playing,
                        paused = string.Equals(status, "paused", StringComparison.Ordinal),
                        stopped = string.Equals(status, "stopped", StringComparison.Ordinal),
                        hasTrack,
                        trackId,
                        track = hasTrack ? new
                        {
                            title = track.TrackTitle,
                            artist = track.ArtistName,
                            album = track.AlbumTitle,
                            albumArtist = track.AlbumArtist,
                            year = track.Year,
                            quality = track.Quality,
                            durationMs = track.Track.Duration.HasValue ? track.Track.Duration.Value : 0
                        } : null,
                        positionMs = Math.Round(positionMs),
                        durationMs = Math.Round(durationMs),
                        progress = durationMs > 0 ? Math.Round(positionMs / durationMs, 6) : 0,
                        volume = Math.Round(this.playbackService.Volume, 4),
                        mute = this.playbackService.Mute,
                        shuffle = this.playbackService.Shuffle,
                        loop = this.playbackService.LoopMode.ToString().ToLowerInvariant(),
                        lyricsVersion,
                        lyricsCount,
                        coverVersion,
                        coverUrl = coverVersion == null ? null : "/api/cover?v=" + Uri.EscapeDataString(coverVersion),
                        theme = new
                        {
                            background = ResolveBrushColor("Brush_WindowHeaderBackground"),
                            primaryText = ResolveBrushColor("Brush_PrimaryText"),
                            secondaryText = ResolveBrushColor("Brush_SecondaryText"),
                            accent = ResolveBrushColor("Brush_Accent"),
                            separator = ResolveBrushColor("Brush_ItemSeparator"),
                            progressTrack = ResolveBrushColor("Brush_SliderBackground"),
                            isLight = SettingsClient.Get<bool>("Appearance", "EnableLightTheme")
                        },
                        serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    return JsonConvert.SerializeObject(payload);
                });
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not build the DSH sync state. Exception: {0}", ex.Message);
                stateJson = "{\"ok\":false,\"error\":\"state-unavailable\"}";
            }

            return stateJson;
        }

        private string BuildLyricsJson()
        {
            lock (this.assetLock)
            {
                var payload = new
                {
                    ok = true,
                    version = this.lyricsVersion,
                    lines = this.lyricLines ?? (IList<LyricLineDto>)new List<LyricLineDto>()
                };

                return JsonConvert.SerializeObject(payload);
            }
        }

        private void WriteCover(NetworkStream stream)
        {
            byte[] bytes;
            string mime;

            lock (this.assetLock)
            {
                bytes = this.coverBytes;
                mime = this.coverMime;
            }

            if (bytes == null || bytes.Length == 0)
            {
                WriteJson(stream, 404, "{\"ok\":false,\"error\":\"no-cover\"}");
                return;
            }

            WriteResponse(stream, 200, "OK", mime ?? "application/octet-stream", bytes);
        }

        private async Task<string> ExecuteCommandAsync(string body)
        {
            JObject request;

            try
            {
                request = JObject.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            }
            catch
            {
                return "{\"ok\":false,\"error\":\"invalid-json\"}";
            }

            string action = (string)request["action"];
            JToken valueToken = request["value"];

            if (string.IsNullOrWhiteSpace(action))
            {
                return "{\"ok\":false,\"error\":\"missing-action\"}";
            }

            action = action.Trim().ToLowerInvariant();

            try
            {
                switch (action)
                {
                    case "playpause":
                        await InvokeOnUiAsync(async () => await this.playbackService.PlayOrPauseAsync()).ConfigureAwait(false);
                        break;

                    case "play":
                        bool playing = OnUi(() => this.playbackService.IsPlaying);
                        if (!playing)
                        {
                            await InvokeOnUiAsync(async () => await this.playbackService.PlayOrPauseAsync()).ConfigureAwait(false);
                        }
                        break;

                    case "pause":
                        bool currentlyPlaying = OnUi(() => this.playbackService.IsPlaying);
                        if (currentlyPlaying)
                        {
                            await InvokeOnUiAsync(async () => await this.playbackService.PlayOrPauseAsync()).ConfigureAwait(false);
                        }
                        break;

                    case "next":
                        await InvokeOnUiAsync(async () => await this.playbackService.PlayNextAsync()).ConfigureAwait(false);
                        break;

                    case "previous":
                        await InvokeOnUiAsync(async () => await this.playbackService.PlayPreviousAsync()).ConfigureAwait(false);
                        break;

                    case "seek":
                        {
                            double progress = valueToken == null ? 0 : valueToken.Value<double>();
                            progress = Math.Max(0, Math.Min(1, progress));
                            await InvokeOnUiAsync(() =>
                            {
                                this.playbackService.SkipProgress(progress);
                                return Task.CompletedTask;
                            }).ConfigureAwait(false);
                            break;
                        }

                    case "volume":
                        {
                            float volume = valueToken == null ? 0 : valueToken.Value<float>();
                            volume = Math.Max(0f, Math.Min(1f, volume));
                            await InvokeOnUiAsync(() =>
                            {
                                this.playbackService.Volume = volume;
                                return Task.CompletedTask;
                            }).ConfigureAwait(false);
                            break;
                        }

                    case "mute":
                        {
                            bool mute = valueToken != null && valueToken.Value<bool>();
                            await InvokeOnUiAsync(() =>
                            {
                                this.playbackService.SetMute(mute);
                                return Task.CompletedTask;
                            }).ConfigureAwait(false);
                            break;
                        }

                    case "shuffle":
                        {
                            bool shuffle = valueToken != null && valueToken.Value<bool>();
                            await InvokeOnUiAsync(async () => await this.playbackService.SetShuffleAsync(shuffle)).ConfigureAwait(false);
                            break;
                        }

                    case "loop":
                        {
                            string loopValue = valueToken == null ? "none" : valueToken.Value<string>();
                            LoopMode mode;
                            if (!Enum.TryParse(loopValue, true, out mode))
                            {
                                mode = LoopMode.None;
                            }

                            await InvokeOnUiAsync(() =>
                            {
                                this.playbackService.LoopMode = mode;
                                return Task.CompletedTask;
                            }).ConfigureAwait(false);
                            break;
                        }

                    default:
                        return "{\"ok\":false,\"error\":\"unknown-action\"}";
                }

                return JsonConvert.SerializeObject(new { ok = true, action });
            }
            catch (Exception ex)
            {
                LogClient.Error("DSH sync command '{0}' failed. Exception: {1}", action, ex.Message);
                return JsonConvert.SerializeObject(new { ok = false, action, error = ex.Message });
            }
        }

        private void SettingsClient_SettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (SettingsClient.IsSettingChanged(e, SettingsSection, EnabledSettingKey))
            {
                if ((bool)e.Entry.Value)
                {
                    this.ScheduleStart();
                }
                else
                {
                    this.Stop();
                }
            }
            else if (SettingsClient.IsSettingChanged(e, SettingsSection, PortSettingKey))
            {
                this.Stop();
                if (SettingsClient.Get<bool>(SettingsSection, EnabledSettingKey))
                {
                    this.Start();
                }
            }
        }

        private void PlaybackService_PlayingTrackChanged(object sender, EventArgs e)
        {
            this.CaptureTrack();
        }

        private void PlaybackService_PlaybackSuccess(object sender, PlaybackSuccessEventArgs e)
        {
            this.CaptureTrack();
        }

        private void CaptureTrack()
        {
            TrackViewModel track = this.playbackService.CurrentTrack;
            string path = track == null ? null : track.Path;

            int generation;
            lock (this.assetLock)
            {
                generation = ++this.trackGeneration;
                this.coverBytes = null;
                this.coverMime = null;
                this.coverVersion = null;
                this.lyricLines = null;
                this.lyricsVersion = null;
            }

            if (string.IsNullOrEmpty(path) || track == null)
            {
                return;
            }

            Task.Run(() => this.RefreshTrackAssetsAsync(track, generation));
        }

        private async Task RefreshTrackAssetsAsync(TrackViewModel track, int generation)
        {
            string path = track.Path;

            byte[] artwork = null;
            try
            {
                artwork = await this.metadataService.GetArtworkAsync(path).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not load artwork for the DSH sync bridge. Exception: {0}", ex.Message);
            }

            lock (this.assetLock)
            {
                if (generation != this.trackGeneration)
                {
                    return;
                }

                this.coverBytes = artwork != null && artwork.Length > 0 ? artwork : null;
                this.coverMime = this.coverBytes == null ? null : DetectImageMime(this.coverBytes);
                this.coverVersion = this.coverBytes == null ? null : HashPath(path) + "-" + this.coverBytes.Length.ToString(CultureInfo.InvariantCulture);
            }

            IList<LyricLineDto> lines = null;
            string version = null;

            try
            {
                var lyrics = await this.lyricsService.GetLyricsAsync(track).ConfigureAwait(false);
                var parsed = this.lyricsService.ParseLyrics(lyrics);

                if (parsed != null && parsed.Count > 0)
                {
                    var converted = new List<LyricLineDto>(parsed.Count);

                    foreach (var line in parsed)
                    {
                        converted.Add(new LyricLineDto
                        {
                            t = (long)line.Time.TotalMilliseconds,
                            text = line.Text ?? string.Empty,
                            timed = line.IsTimed
                        });
                    }

                    lines = converted;
                    version = HashPath(path) + "-" + HashLyrics(converted);
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not load lyrics for the DSH sync bridge. Exception: {0}", ex.Message);
            }

            lock (this.assetLock)
            {
                if (generation != this.trackGeneration)
                {
                    return;
                }

                this.lyricLines = lines;
                this.lyricsVersion = version;
            }
        }

        private static string HashPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "unknown";
            }

            return ShortHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()), 10);
        }

        private static string HashLyrics(IList<LyricLineDto> lines)
        {
            var builder = new StringBuilder();

            foreach (var line in lines)
            {
                builder.Append(line.t.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(line.text);
                builder.Append('\n');
            }

            return ShortHash(Encoding.UTF8.GetBytes(builder.ToString()), 8);
        }

        private static string ShortHash(byte[] bytes, int length)
        {
            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);

                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString(0, Math.Min(length, builder.Length));
            }
        }

        /// <summary>
        /// Resolves a theme brush to a CSS rgba() color. EchoStem's brushes are
        /// dynamic resources, so the plugin reads the live colors from the
        /// running player instead of hard-coding its own palette.
        /// </summary>
        private static string ResolveBrushColor(string resourceKey)
        {
            Application app = Application.Current;

            if (app == null)
            {
                return null;
            }

            var brush = app.TryFindResource(resourceKey) as SolidColorBrush;

            if (brush == null)
            {
                return null;
            }

            Color color = brush.Color;
            double alpha = (color.A / 255.0) * brush.Opacity;

            return string.Format(
                CultureInfo.InvariantCulture,
                "rgba({0},{1},{2},{3:0.###})",
                color.R,
                color.G,
                color.B,
                alpha);
        }

        private static string DetectImageMime(byte[] data)        {
            if (data == null || data.Length < 12)
            {
                return "application/octet-stream";
            }

            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            {
                return "image/png";
            }

            if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F')
            {
                return "image/gif";
            }

            if (data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
                data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
            {
                return "image/webp";
            }

            if (data[0] == 'B' && data[1] == 'M')
            {
                return "image/bmp";
            }

            return "application/octet-stream";
        }

        private static T OnUi<T>(Func<T> func)
        {
            Application app = Application.Current;

            if (app == null || app.Dispatcher.CheckAccess())
            {
                return func();
            }

            return (T)app.Dispatcher.Invoke(func);
        }

        private static Task InvokeOnUiAsync(Func<Task> action)
        {
            Application app = Application.Current;

            if (app == null || app.Dispatcher.CheckAccess())
            {
                return action();
            }

            return app.Dispatcher.InvokeAsync(action).Task.Unwrap();
        }

        private static HttpRequest ReadRequest(NetworkStream stream)
        {
            var headerBuffer = new MemoryStream();
            var readBuffer = new byte[4096];
            int headerEnd = -1;

            while (headerEnd < 0)
            {
                int read = stream.Read(readBuffer, 0, readBuffer.Length);

                if (read <= 0)
                {
                    return null;
                }

                headerBuffer.Write(readBuffer, 0, read);

                if (headerBuffer.Length > MaxHeaderBytes)
                {
                    throw new InvalidOperationException("Request headers are too large.");
                }

                headerEnd = FindHeaderEnd(headerBuffer.GetBuffer(), (int)headerBuffer.Length);
            }

            byte[] allBytes = headerBuffer.ToArray();
            string headerText = Encoding.UTF8.GetString(allBytes, 0, headerEnd);
            string[] headerLines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);

            if (headerLines.Length == 0)
            {
                return null;
            }

            string[] requestParts = headerLines[0].Split(' ');

            if (requestParts.Length < 2)
            {
                return null;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 1; index < headerLines.Length; index++)
            {
                int separator = headerLines[index].IndexOf(':');

                if (separator > 0)
                {
                    headers[headerLines[index].Substring(0, separator).Trim()] = headerLines[index].Substring(separator + 1).Trim();
                }
            }

            string target = requestParts[1];
            string path = target;
            string query = string.Empty;

            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(target);
                path = uri.AbsolutePath;
                query = uri.Query;
            }
            else
            {
                int queryStart = target.IndexOf('?');

                if (queryStart >= 0)
                {
                    path = target.Substring(0, queryStart);
                    query = target.Substring(queryStart);
                }
            }

            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            if (string.IsNullOrEmpty(query))
            {
                query = string.Empty;
            }

            int contentLength = 0;

            string contentLengthValue;
            if (headers.TryGetValue("Content-Length", out contentLengthValue))
            {
                int.TryParse(contentLengthValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength);
            }

            if (contentLength < 0 || contentLength > MaxBodyBytes)
            {
                throw new InvalidOperationException("Request body is too large.");
            }

            byte[] body = new byte[contentLength];
            int bodyAlreadyRead = allBytes.Length - (headerEnd + 4);
            int copied = Math.Min(bodyAlreadyRead, contentLength);

            if (copied > 0)
            {
                Buffer.BlockCopy(allBytes, headerEnd + 4, body, 0, copied);
            }

            int bodyOffset = copied;

            while (bodyOffset < contentLength)
            {
                int read = stream.Read(body, bodyOffset, contentLength - bodyOffset);

                if (read <= 0)
                {
                    break;
                }

                bodyOffset += read;
            }

            return new HttpRequest
            {
                Method = requestParts[0],
                Path = path,
                Query = query,
                Headers = headers,
                Body = body
            };
        }

        private static int FindHeaderEnd(byte[] buffer, int length)
        {
            for (int index = 0; index <= length - 4; index++)
            {
                if (buffer[index] == '\r' && buffer[index + 1] == '\n' &&
                    buffer[index + 2] == '\r' && buffer[index + 3] == '\n')
                {
                    return index;
                }
            }

            return -1;
        }

        private static void WriteJson(NetworkStream stream, int status, string json)
        {
            WriteResponse(stream, status, status == 200 ? "OK" : "Not Found", "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
        }

        private static void WriteResponse(NetworkStream stream, int status, string statusText, string contentType, byte[] body)
        {
            if (body == null)
            {
                body = new byte[0];
            }

            string headers =
                "HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture) + " " + statusText + "\r\n" +
                "Content-Type: " + contentType + "\r\n" +
                "Content-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type\r\n" +
                "Access-Control-Max-Age: 86400\r\n" +
                "Cache-Control: no-store, no-cache, must-revalidate\r\n" +
                "Connection: close\r\n\r\n";

            byte[] headerBytes = Encoding.UTF8.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);

            if (body.Length > 0)
            {
                stream.Write(body, 0, body.Length);
            }

            stream.Flush();
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            SettingsClient.SettingChanged -= this.SettingsClient_SettingChanged;
            this.playbackService.PlayingTrackChanged -= this.PlaybackService_PlayingTrackChanged;
            this.playbackService.PlaybackSuccess -= this.PlaybackService_PlaybackSuccess;
            this.Stop();
        }

        private sealed class HttpRequest
        {
            public string Method { get; set; }
            public string Path { get; set; }
            public string Query { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public byte[] Body { get; set; }

            public string BodyText
            {
                get { return this.Body == null || this.Body.Length == 0 ? string.Empty : Encoding.UTF8.GetString(this.Body); }
            }
        }

        private sealed class LyricLineDto
        {
            public long t { get; set; }
            public string text { get; set; }
            public bool timed { get; set; }
        }
    }
}
