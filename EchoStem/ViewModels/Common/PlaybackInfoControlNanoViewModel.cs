using EchoStem.Services.Metadata;
using EchoStem.Services.Playback;
using EchoStem.Services.Scrobbling;

namespace EchoStem.ViewModels.Common
{
    public class PlaybackInfoControlNanoViewModel : PlaybackInfoControlViewModel
    {
        public PlaybackInfoControlNanoViewModel(
            IPlaybackService playbackService, 
            IMetadataService metadataService,
            IScrobblingService scrobblingService) : base(
            playbackService, 
            metadataService,
            scrobblingService
            )
        {
        }
    }
}