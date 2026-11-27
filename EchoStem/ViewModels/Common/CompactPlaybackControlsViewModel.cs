using EchoStem.ViewModels;
using EchoStem.Services.Playback;
using CommonServiceLocator;
using Prism.Events;

namespace EchoStem.ViewModels.Common
{
    public class CompactPlaybackControlsViewModel : PlaybackControlsViewModel
    {
        public CompactPlaybackControlsViewModel() : base(ServiceLocator.Current.GetInstance<IPlaybackService>(), ServiceLocator.Current.GetInstance<IEventAggregator>())
        {
        }
    }
}
