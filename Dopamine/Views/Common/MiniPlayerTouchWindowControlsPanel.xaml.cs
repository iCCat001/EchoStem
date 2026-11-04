using Dopamine.Core.Base;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Dopamine.Views.Common
{
    /// <summary>
    /// Touch optimization: the panel which is revealed to the left of the mini player and contains
    /// the window buttons (switch player / minimize / close). It is only shown while touch
    /// optimization is enabled: in mouse mode those buttons are revealed by hovering the mini player
    /// instead. The buttons are stacked vertically by default; the Nano Player is only 48 px high,
    /// so it lays them out horizontally instead of growing its window.
    /// </summary>
    public partial class MiniPlayerTouchWindowControlsPanel : UserControl
    {
        public static readonly DependencyProperty PanelOrientationProperty =
            DependencyProperty.Register(nameof(PanelOrientation), typeof(Orientation), typeof(MiniPlayerTouchWindowControlsPanel), new PropertyMetadata(Orientation.Vertical, OnLayoutPropertyChanged));

        /// <summary>
        /// When true, the vertically stacked buttons share the full height of the panel, so their
        /// highlights touch the top and bottom edges of the window instead of leaving a gap.
        /// </summary>
        public static readonly DependencyProperty FillVerticallyProperty =
            DependencyProperty.Register(nameof(FillVertically), typeof(bool), typeof(MiniPlayerTouchWindowControlsPanel), new PropertyMetadata(false, OnLayoutPropertyChanged));

        /// <summary>Size of a single button along the panel's short side.</summary>
        public static readonly DependencyProperty ButtonSizeProperty =
            DependencyProperty.Register(nameof(ButtonSize), typeof(double), typeof(MiniPlayerTouchWindowControlsPanel), new PropertyMetadata(Constants.MiniPlayerTouchButtonSize, OnLayoutPropertyChanged));

        /// <summary>Orientation of the window buttons inside the panel.</summary>
        public Orientation PanelOrientation
        {
            get { return (Orientation)GetValue(PanelOrientationProperty); }
            set { SetValue(PanelOrientationProperty, value); }
        }

        /// <summary>True when the buttons have to fill the panel's height.</summary>
        public bool FillVertically
        {
            get { return (bool)GetValue(FillVerticallyProperty); }
            set { SetValue(FillVerticallyProperty, value); }
        }

        /// <summary>Size of a single button.</summary>
        public double ButtonSize
        {
            get { return (double)GetValue(ButtonSizeProperty); }
            set { SetValue(ButtonSizeProperty, value); }
        }

        public MiniPlayerTouchWindowControlsPanel()
        {
            InitializeComponent();

            this.Loaded += (_, __) => this.UpdateButtonLayout();
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = d as MiniPlayerTouchWindowControlsPanel;

            if (panel != null && panel.IsLoaded)
            {
                panel.UpdateButtonLayout();
            }
        }

        private void MiniPlayerTouchWindowControlsPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateButtonLayout();
        }

        /// <summary>
        /// Applies the button sizes and the spacing between them. Vertical panels use the panel width
        /// (the highlight then reaches the window edge) and a spacing of exactly
        /// <see cref="Constants.MiniPlayerTouchButtonSpacing"/>; horizontal panels keep square buttons
        /// because their height is fixed.
        /// </summary>
        private void UpdateButtonLayout()
        {
            if (this.ButtonsPanel == null)
            {
                return;
            }

            this.ButtonsPanel.Orientation = this.PanelOrientation;

            Button[] buttons = { this.PART_TogglePlayer, this.PART_Minimize, this.PART_Close };

            double spacing = Constants.MiniPlayerTouchButtonSpacing;
            bool isVertical = this.PanelOrientation == Orientation.Vertical;
            double width = isVertical
                ? (this.ActualWidth > 0 ? this.ActualWidth : Constants.MiniPlayerTouchPanelWidth)
                : this.ButtonSize;
            double height = this.ButtonSize;

            if (isVertical)
            {
                if (this.FillVertically)
                {
                    double available = this.ActualHeight > 0 ? this.ActualHeight : Constants.MiniPlayerTouchPanelHeight;
                    height = Math.Max(this.ButtonSize, (available - (buttons.Length - 1) * spacing) / buttons.Length);
                }
            }
            else
            {
                // Horizontal panel (Nano Player): the buttons fill the bar's height, so there is no
                // gap above or below them.
                height = this.ActualHeight > 0 ? this.ActualHeight : this.ButtonSize;
            }

            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];

                if (button == null)
                {
                    continue;
                }

                button.Width = width;
                button.Height = height;
                button.Margin = index == 0
                    ? new Thickness(0)
                    : (isVertical ? new Thickness(0, spacing, 0, 0) : new Thickness(spacing, 0, 0, 0));
            }
        }
    }
}
