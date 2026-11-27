using EchoStem.ViewModels.Common.Base;
using EchoStem.Services.Playback;

namespace EchoStem.ViewModels.MiniPlayer
{
    public class MiniPlayerPlaylistViewModel : NowPlayingViewModelBase
    {
        public MiniPlayerPlaylistViewModel(IPlaybackService playbackService) : base(playbackService)
        {
        }
    }
}
