using CommonServiceLocator;
using EchoStem.Services.Metadata;
using EchoStem.Services.Playback;
using EchoStem.Services.Scrobbling;

namespace EchoStem.ViewModels.Common
{
    public class CoverPlaybackInfoControlViewModel : PlaybackInfoControlViewModel
    {
        public CoverPlaybackInfoControlViewModel() : base(
            ServiceLocator.Current.GetInstance<IPlaybackService>(), 
            ServiceLocator.Current.GetInstance<IMetadataService>(),
            ServiceLocator.Current.GetInstance<IScrobblingService>())
        {
        }
    }
}