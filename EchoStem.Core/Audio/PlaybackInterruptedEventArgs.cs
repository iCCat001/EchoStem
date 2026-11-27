using System;

namespace EchoStem.Core.Audio
{
    public class PlaybackInterruptedEventArgs : EventArgs
    {
        public string Message { get; set; }
    }
}
