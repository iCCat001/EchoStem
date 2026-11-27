using EchoStem.ViewModels;
using EchoStem.Services.Playback;
using CommonServiceLocator;

namespace EchoStem.ViewModels.Common
{
    public class PopupVolumeControlsViewModel : VolumeControlsViewModel
    {
        public PopupVolumeControlsViewModel() : base(ServiceLocator.Current.GetInstance<IPlaybackService>())
        {
        }
    }
}
