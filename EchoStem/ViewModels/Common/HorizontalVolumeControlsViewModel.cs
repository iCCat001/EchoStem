using EchoStem.ViewModels;
using EchoStem.Services.Playback;
using CommonServiceLocator;

namespace EchoStem.ViewModels.Common
{
    public class HorizontalVolumeControlsViewModel : VolumeControlsViewModel
    {
        public HorizontalVolumeControlsViewModel() : base(ServiceLocator.Current.GetInstance<IPlaybackService>())
        {
        }
    }
}