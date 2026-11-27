using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EchoStem.Controls
{
    /// <summary>
    /// The EchoStem logo, drawn as vectors so it can follow the accent colour of the selected theme
    /// and stay crisp at any size. The shapes come from Resources/Styles/Geometries.xaml.
    /// </summary>
    public partial class BrandMark : UserControl
    {
        private static readonly Brush DefaultFrameBrush = CreateFrozenBrush(0x8C, 0xFF, 0xFF, 0xFF);

        /// <summary>Colour of the rounded square background (normally the theme accent colour).</summary>
        public Brush Accent
        {
            get { return (Brush)GetValue(AccentProperty); }
            set { SetValue(AccentProperty, value); }
        }

        public static readonly DependencyProperty AccentProperty =
            DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(BrandMark), new PropertyMetadata(Brushes.Orange));

        /// <summary>Colour of the thin inner frame.</summary>
        public Brush FrameBrush
        {
            get { return (Brush)GetValue(FrameBrushProperty); }
            set { SetValue(FrameBrushProperty, value); }
        }

        public static readonly DependencyProperty FrameBrushProperty =
            DependencyProperty.Register(nameof(FrameBrush), typeof(Brush), typeof(BrandMark), new PropertyMetadata(DefaultFrameBrush));

        /// <summary>Colour of the bars.</summary>
        public Brush BarsBrush
        {
            get { return (Brush)GetValue(BarsBrushProperty); }
            set { SetValue(BarsBrushProperty, value); }
        }

        public static readonly DependencyProperty BarsBrushProperty =
            DependencyProperty.Register(nameof(BarsBrush), typeof(Brush), typeof(BrandMark), new PropertyMetadata(Brushes.White));

        public BrandMark()
        {
            InitializeComponent();
        }

        private static Brush CreateFrozenBrush(byte alpha, byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
