using EchoStem.ViewModels.Common.Base;
using Prism.Ioc;

namespace EchoStem.ViewModels.NowPlaying
{
    public class NowPlayingShowcaseViewModel : ContextMenuViewModelBase
    {
        public NowPlayingShowcaseViewModel(IContainerProvider container) : base(container)
        {
        }

        protected override void SearchOnline(string id)
        {
            // No implementation required here
        }
    }
}
