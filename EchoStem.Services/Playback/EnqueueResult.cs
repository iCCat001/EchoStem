using EchoStem.Services.Entities;
using System.Collections.Generic;

namespace EchoStem.Services.Playback
{
    public class EnqueueResult
    {
        public bool IsSuccess { get; set; }

        public IList<TrackViewModel> EnqueuedTracks { get; set; }
    }
}
