using EchoStem.Core.Base;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EchoStem.Controls
{
    public partial class FullLogoSmall : UserControl
    {
        public Brush Accent
        {
            get { return (Brush)GetValue(AccentProperty); }

            set { SetValue(AccentProperty, value); }
        }

        public static readonly DependencyProperty AccentProperty = 
            DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(FullLogoSmall), new PropertyMetadata(null));

        public string ApplicationName
        {
            get { return ProductInformation.ApplicationName; }
        }

        public FullLogoSmall()
        {
            InitializeComponent();
        }
    }
}
