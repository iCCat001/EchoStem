using EchoStem.ViewModels.Common.Base;
using EchoStem.Core.Prism;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;

namespace EchoStem.ViewModels.MiniPlayer
{
    public class NanoPlayerViewModel : MiniPlayerViewModelBase
    {
        private IEventAggregator eventAggregator;

        public DelegateCommand<bool?> NanoPlayerPlaylistButtonCommand { get; set; }

        public NanoPlayerViewModel(IContainerProvider container) : base(container)
        {
            this.eventAggregator = container.Resolve<IEventAggregator>();

            this.NanoPlayerPlaylistButtonCommand = new DelegateCommand<bool?>(iIsPlaylistButtonChecked =>
            {
                this.IsPlaylistVisible = iIsPlaylistButtonChecked.Value;
            });
            ApplicationCommands.NanoPlayerPlaylistButtonCommand.RegisterCommand(this.NanoPlayerPlaylistButtonCommand);
        }
    }
}
