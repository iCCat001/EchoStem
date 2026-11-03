using System;

namespace Dopamine.Services.Sync
{
    /// <summary>
    /// Optional local bridge which lets the DeepSeek Harness (DSH) Web UI read the
    /// current playback state and send simple playback commands back to EchoStem.
    /// The bridge only listens on the loopback interface and is only active while
    /// the "Support DSH plugin sync" setting is enabled.
    /// </summary>
    public interface IDshSyncService
    {
        /// <summary>Gets whether the local HTTP listener is currently running.</summary>
        bool IsRunning { get; }

        /// <summary>Gets the port the bridge listens on (or would listen on).</summary>
        int Port { get; }

        /// <summary>Starts the bridge when it is not already running.</summary>
        void Start();

        /// <summary>Stops the bridge.</summary>
        void Stop();
    }
}
