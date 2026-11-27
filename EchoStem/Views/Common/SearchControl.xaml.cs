using CommonServiceLocator;
using EchoStem.Core.Prism;
using Prism.Events;
using System.Windows.Controls;

namespace EchoStem.Views.Common
{
    public partial class SearchControl : UserControl
    {
        public SearchControl()
        {
            InitializeComponent();

            IEventAggregator eventAggregator = ServiceLocator.Current.GetInstance<IEventAggregator>();

            eventAggregator.GetEvent<FocusSearchBox>().Subscribe((_) => this.SearchBox.SetKeyboardFocus());
        }
    }
}
