using EchoStem.Services.Playback;
using EchoStem.Services.Playback;
using CommonServiceLocator;

namespace EchoStem.ViewModels.Common
{
    public class ProgressControlsThinViewModel : ProgressControlsViewModel
    {
        public ProgressControlsThinViewModel() : base(ServiceLocator.Current.GetInstance<IPlaybackService>())
        {
        }
    }
}