using EchoStem.Services.Playback;
using CommonServiceLocator;

namespace EchoStem.ViewModels
{
    public class VerticalVolumeControlsViewModel : VolumeControlsViewModel
    {
        // Workaround to have inheritance with dependency injection
        public VerticalVolumeControlsViewModel() : base(ServiceLocator.Current.GetInstance<IPlaybackService>())
        {
        }
    }
}