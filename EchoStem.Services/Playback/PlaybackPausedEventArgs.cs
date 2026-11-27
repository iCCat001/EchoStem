using System;

namespace EchoStem.Services.Playback
{
    public class PlaybackPausedEventArgs : EventArgs
    {
        public bool IsSilent { get; set; }
    }
}