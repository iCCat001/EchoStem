using System;

namespace EchoStem.Services.Playback
{
    public class PlaybackSuccessEventArgs : EventArgs
    {
        public bool IsPlayingPreviousTrack { get; set; }

        public bool IsSilent{ get; set; }
}
}