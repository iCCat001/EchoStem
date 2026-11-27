using EchoStem.ViewModels.Common.Base;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EchoStem.ViewModels.NowPlaying
{
    public class NowPlayingPlaylistViewModel : ContextMenuViewModelBase
    {
        public NowPlayingPlaylistViewModel(IContainerProvider container) : base(container)
        {
        }

        protected override void SearchOnline(string id)
        {
            // No implementation required here
        }
    }
}
