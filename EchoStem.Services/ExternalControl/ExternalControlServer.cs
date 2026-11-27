using EchoStem.Core.Audio;
using EchoStem.Core.Extensions;
using EchoStem.Services.Cache;
using EchoStem.Services.Playback;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EchoStem.Services.ExternalControl
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Reentrant)]
    internal class ExternalControlServer : IExternalControlServer, IDisposable , IFftDataServer
    {
        private const int FftDataLength = 256 * 4;

        // 2.0.12: the FFT bridge uses the new EchoStem names. For one version the old Dopamine names
        // are also written, so existing external control clients keep working.
        // TODO (2.0.13): remove the legacy names - see .dsh/TODO-2.0.13.md.
        private const bool LegacyExternalControlNames = true;
        private const string FftDataMemoryName = "EchoStemFftDataMemory";
        private const string FftDataMemoryMutexName = "EchoStemFftDataMemoryMutex";
        private const string LegacyFftDataMemoryName = "DopamineFftDataMemory";
        private const string LegacyFftDataMemoryMutexName = "DopamineFftDataMemoryMutex";

        private readonly FftAggregator fftAggregator = new FftAggregator(512);
        private readonly DispatcherTimer fftProviderDataTimer;
        private IPlayer subscribedPlayer;
        private readonly float[] fftDataBuffer = new float[FftDataLength / 4];
        private readonly byte[] fftDataBufferBytes = new byte[FftDataLength];
        private readonly MemoryMappedFile fftDataMemoryMappedFile;
        private readonly MemoryMappedViewStream fftDataMemoryMappedFileStream;
        private readonly BinaryWriter fftDataMemoryMappedFileStreamWriter;
        private readonly Mutex fftDataMemoryMappedFileMutex;
        private readonly MemoryMappedFile legacyFftDataMemoryMappedFile;
        private readonly MemoryMappedViewStream legacyFftDataMemoryStream;
        private readonly BinaryWriter legacyFftDataMemoryWriter;
        private readonly Mutex legacyFftDataMemoryMutex;

        private readonly Dictionary<string, IExternalControlServerCallback> clients = new Dictionary<string, IExternalControlServerCallback>();
        private readonly Stack<string> deadClients = new Stack<string>();
        private readonly object clientsLock = new object();

        private readonly IPlaybackService playbackService;
        private readonly ICacheService cacheService;
   
        public ExternalControlServer(IPlaybackService playbackService, ICacheService cacheService)
        {
            this.playbackService = playbackService;
            this.cacheService = cacheService;

            this.fftProviderDataTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(2) };
            this.fftProviderDataTimer.Tick += FftProviderDataTimerElapsed;

            fftDataMemoryMappedFile = MemoryMappedFile.CreateOrOpen(FftDataMemoryName, FftDataLength, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.DelayAllocatePages, null, HandleInheritability.None);
            fftDataMemoryMappedFileStream = fftDataMemoryMappedFile.CreateViewStream(0, FftDataLength, MemoryMappedFileAccess.ReadWrite);
            fftDataMemoryMappedFileStreamWriter = new BinaryWriter(fftDataMemoryMappedFileStream);
            fftDataMemoryMappedFileMutex = new Mutex(true, FftDataMemoryMutexName);
            fftDataMemoryMappedFileMutex.ReleaseMutex();

            if (LegacyExternalControlNames)
            {
                try
                {
                    legacyFftDataMemoryMappedFile = MemoryMappedFile.CreateOrOpen(LegacyFftDataMemoryName, FftDataLength, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.DelayAllocatePages, null, HandleInheritability.None);
                    legacyFftDataMemoryStream = legacyFftDataMemoryMappedFile.CreateViewStream(0, FftDataLength, MemoryMappedFileAccess.ReadWrite);
                    legacyFftDataMemoryWriter = new BinaryWriter(legacyFftDataMemoryStream);
                    legacyFftDataMemoryMutex = new Mutex(true, LegacyFftDataMemoryMutexName);
                    legacyFftDataMemoryMutex.ReleaseMutex();
                }
                catch (Exception)
                {
                    // The compatibility bridge is optional: never let it break the new one.
                }
            }
        }
    
        private bool m_disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;

            try { fftDataMemoryMappedFileMutex?.Dispose(); } catch (Exception) { }
            try { fftDataMemoryMappedFileStreamWriter?.Dispose(); } catch (Exception) { }
            try { fftDataMemoryMappedFileStream?.Dispose(); } catch (Exception) { }
            try { fftDataMemoryMappedFile?.Dispose(); } catch (Exception) { }

            try { legacyFftDataMemoryMutex?.Dispose(); } catch (Exception) { }
            try { legacyFftDataMemoryWriter?.Dispose(); } catch (Exception) { }
            try { legacyFftDataMemoryStream?.Dispose(); } catch (Exception) { }
            try { legacyFftDataMemoryMappedFile?.Dispose(); } catch (Exception) { }
        }

        ~ExternalControlServer()
        {
            Dispose(false);
        }
       
        [OperationBehavior(ReleaseInstanceMode = ReleaseInstanceMode.None)]
        public string RegisterClient()
        {
            var context = OperationContext.Current;
            var sessionId = context.SessionId;
            try
            {
                var callback = context.GetCallbackChannel<IExternalControlServerCallback>();

                clients.TryRemove(context.SessionId);
                clients.Add(sessionId, callback);

                return sessionId;
            }
            catch (Exception)
            {
                clients.TryRemove(sessionId);
                return string.Empty;
            }
        }

        [OperationBehavior]
        public void DeregisterClient(string sessionId)
        {
            clients.TryRemove(sessionId);
        }

        [OperationBehavior]
        public void SendHeartbeat() { }

        [OperationBehavior]
        public async Task PlayNext() => await this.playbackService.PlayNextAsync();

        [OperationBehavior]
        public async Task PlayPrevious() => await this.playbackService.PlayPreviousAsync();

        [OperationBehavior]
        public void SetMute(bool mute) => this.playbackService.SetMute(mute);

        [OperationBehavior]
        public Task PlayOrPause() => this.playbackService.PlayOrPauseAsync();

        [OperationBehavior]
        public bool GetIsStopped() => this.playbackService.IsStopped;

        [OperationBehavior]
        public bool GetIsPlaying() => this.playbackService.IsPlaying;

        [OperationBehavior]
        public double GetProgress() => this.playbackService.Progress;

        [OperationBehavior]
        public void SetProgress(double progress) => this.playbackService.SkipProgress(progress);

        [OperationBehavior]
        public ExternalTrack GetCurrenTrack() => new ExternalTrack(this.playbackService.CurrentTrack);

        [OperationBehavior]
        public string GetCurrentTrackArtworkPath(string artworkId) => this.cacheService.GetCachedArtworkPath(artworkId);
     
        [OperationBehavior]
        public int GetFftDataSize() => FftDataLength;

        [OperationBehavior]
        public async Task GetFftData()
        {
            this.fftProviderDataTimer.Stop();
            this.fftProviderDataTimer.Start();
            TryAddInputStreamHandler();

            await Task.Run(() =>
            {
                this.fftAggregator.GetMagnitudes(fftDataBuffer);

                Buffer.BlockCopy(fftDataBuffer, 0, fftDataBufferBytes, 0, fftDataBufferBytes.Length);
                WriteFftData(fftDataMemoryMappedFileMutex, fftDataMemoryMappedFileStreamWriter);
                WriteFftData(legacyFftDataMemoryMutex, legacyFftDataMemoryWriter);
            });
        }

        internal void Open()
        {
            this.playbackService.PlaybackSuccess += PlaybackSuccessCallback;
            this.playbackService.PlaybackStopped += PlaybackStoppedCallback;
            this.playbackService.PlaybackPaused += PlaybackPausedCallBack;
            this.playbackService.PlaybackResumed += PlaybackResumedCallBack;
            this.playbackService.PlaybackProgressChanged += PlaybackProgressChangedCallBack;
            this.playbackService.PlaybackVolumeChanged += PlaybackVolumeChangedCallBack;
            this.playbackService.PlaybackMuteChanged += PlaybackMuteCallBack;
            this.playbackService.PlayingTrackChanged += PlayingTrackChangedCallback;
        }

        internal void Close()
        {
            this.playbackService.PlaybackSuccess -= PlaybackSuccessCallback;
            this.playbackService.PlaybackStopped -= PlaybackStoppedCallback;
            this.playbackService.PlaybackPaused -= PlaybackPausedCallBack;
            this.playbackService.PlaybackResumed -= PlaybackResumedCallBack;
            this.playbackService.PlaybackProgressChanged -= PlaybackProgressChangedCallBack;
            this.playbackService.PlaybackVolumeChanged -= PlaybackVolumeChangedCallBack;
            this.playbackService.PlaybackMuteChanged -= PlaybackMuteCallBack;
            this.playbackService.PlayingTrackChanged -= PlayingTrackChangedCallback;
        }

        private void PlayingTrackChangedCallback(object sender, EventArgs e)
        {
            ProxyMethod(nameof(IExternalControlServerCallback.RaiseEventPlayingTrackChangedAsync));
        }

        private void PlaybackMuteCallBack(object sender, EventArgs e)
        {
            ProxyMethod(nameof(IExternalControlServerCallback.RaiseEventPlaybackMuteChangedAsync));
        }

        private void PlaybackVolumeChangedCallBack(object sender, PlaybackVolumeChangedEventArgs e)
        {
            ProxyMethod(nameof(IExternalControlServerCallback.RaiseEventPlaybackVolumeChangedAsync));
        }

        private void PlaybackProgressChangedCallBack(object sender, EventArgs e)
        {
            ProxyMethod(nameof(IExternalControlServerCallback.RaiseEventPlaybackProgressChangedAsync));
        }

        private void PlaybackResumedCallBack(object sender, EventArgs e)
        {
            ProxyMethod(nameof(IExternalControlServerCallback.RaiseEventPlaybackResumedAsync));
        }

        private void PlaybackPausedCallBack(object sender, EventArgs e)
        {
            ProxyMethod(nameof(IExternalControlServerCallback.RaiseEventPlaybackPausedAsync));
        }

        private void PlaybackStoppedCallback(object sender, EventArgs e)
        {
            ProxyMethod(nameof(IExternalControlServerCallback.RaiseEventPlaybackStoppedAsync));
        }

        private void PlaybackSuccessCallback(object sender, PlaybackSuccessEventArgs e)
        {
            ProxyMethod(nameof(IExternalControlServerCallback.RaiseEventPlaybackSuccessAsync));
        }

        private void ProxyMethod(string methodName)
        {
            MethodInfo methodInfo = typeof(IExternalControlServerCallback).GetMethod(methodName);

            lock (clientsLock)
            {
                foreach (var client in clients)
                {
                    try
                    {
                        methodInfo.Invoke(client.Value, null);
                    }
                    catch (Exception)
                    {
                        deadClients.Push(client.Key);
                    }
                }

                foreach (var c in deadClients)
                {
                    clients.Remove(c);
                }
                deadClients.Clear();
            }
        }

        private void TryAddInputStreamHandler()
        {
            IPlayer player = this.playbackService.Player;

            if (player == null || ReferenceEquals(player, this.subscribedPlayer))
            {
                return;
            }

            // The player instance is replaced for every track: move the subscription along.
            if (this.subscribedPlayer != null)
            {
                this.subscribedPlayer.AudioBlockRead -= this.InputBlockRead;
            }

            player.AudioBlockRead += this.InputBlockRead;
            this.subscribedPlayer = player;
        }

        private void TryRemoveInputStreamHandler()
        {
            if (this.subscribedPlayer == null)
            {
                return;
            }

            this.subscribedPlayer.AudioBlockRead -= this.InputBlockRead;
            this.subscribedPlayer = null;
        }

        /// <summary>
        /// Writes one FFT block to a shared memory buffer. Both buffers are written inside their own
        /// lock so an external reader never sees a half written block. The legacy buffer may not
        /// exist (compatibility bridge creation failed): the write is skipped then.
        /// </summary>
        private void WriteFftData(Mutex mutex, BinaryWriter writer)
        {
            if (mutex == null || writer == null)
            {
                return;
            }

            mutex.WaitOne();

            try
            {
                writer.Seek(0, SeekOrigin.Begin);
                writer.Write(fftDataBufferBytes);
                writer.Flush();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        private void InputBlockRead(object sender, AudioBlockReadEventArgs e)
        {
            try
            {
                for (int i = 0; i + e.Channels - 1 < e.Count; i += e.Channels)
                {
                    int index = e.Offset + i;
                    float left = e.Buffer[index];
                    float right = e.Channels >= 2 ? e.Buffer[index + 1] : e.Buffer[index];

                    this.fftAggregator.Add(0.5f * (left + right));
                }
            }
            catch (Exception)
            {
                // Intended suppression 
            }
        }

        private void FftProviderDataTimerElapsed(object sender, EventArgs e)
        {
            this.fftProviderDataTimer.Stop();
            this.TryRemoveInputStreamHandler();
        }
    }
}