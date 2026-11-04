using Digimezzo.Foundation.Core.Settings;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Services.Shell;
using Dopamine.Utils;
using Prism.Commands;
using System;
using System.Windows;
using System.Windows.Input;

namespace Dopamine.Views.Common.Base
{
    public class MiniPlayerViewBase : TracksViewBase
    {
        private bool isMiniPlayerPositionLocked;
        private bool isSubscribed;

        public DelegateCommand ToggleMiniPlayerPositionLockedCommand { get; set; }

        public MiniPlayerViewBase() : base()
        {
            this.ToggleMiniPlayerPositionLockedCommand = new DelegateCommand(() => this.isMiniPlayerPositionLocked = !this.isMiniPlayerPositionLocked);
            Core.Prism.ApplicationCommands.ToggleMiniPlayerPositionLockedCommand.RegisterCommand(this.ToggleMiniPlayerPositionLockedCommand);
            this.isMiniPlayerPositionLocked = SettingsClient.Get<bool>("Behaviour", "MiniPlayerPositionLocked");

            // Touch optimization: the mini players adapt their layout (a panel with the window buttons
            // appears on the left, and the mouse-only hover window controls are hidden).
            this.Loaded += this.MiniPlayerViewBase_Loaded;
            this.Unloaded += this.MiniPlayerViewBase_Unloaded;
        }

        protected void MouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (!this.isMiniPlayerPositionLocked)
            {
                // We need to use a custom function because the built-in DragMove function causes 
                // flickering when blurring the cover art and releasing the mouse button after a drag.
                if (e.ClickCount == 1) WindowUtils.MoveWindow(Application.Current.MainWindow);
            }
        }

        private void MiniPlayerViewBase_Loaded(object sender, RoutedEventArgs e)
        {
            this.UpdateTouchWindowControlsVisibility();

            if (!this.isSubscribed)
            {
                this.isSubscribed = true;
                MiniPlayerTouchPanelManager.ExpansionChanged += this.MiniPlayerTouchPanelManager_ExpansionChanged;
                SettingsClient.SettingChanged += this.SettingsClient_SettingChanged;
            }

            this.UpdateTouchWindowControlsPanelLayout(MiniPlayerTouchPanelManager.IsExpanded);
        }

        private void MiniPlayerViewBase_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!this.isSubscribed)
            {
                return;
            }

            this.isSubscribed = false;
            MiniPlayerTouchPanelManager.ExpansionChanged -= this.MiniPlayerTouchPanelManager_ExpansionChanged;
            SettingsClient.SettingChanged -= this.SettingsClient_SettingChanged;
        }

        private void MiniPlayerTouchPanelManager_ExpansionChanged(object sender, EventArgs e)
        {
            this.UpdateTouchWindowControlsPanelLayout(MiniPlayerTouchPanelManager.IsExpanded);
        }

        private void SettingsClient_SettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (!SettingsClient.IsSettingChanged(e, "Features", "TouchOptimization"))
            {
                return;
            }

            if (!TouchOptimization.IsEnabled)
            {
                // Collapse first, so the window returns to its original geometry.
                MiniPlayerTouchPanelManager.SetExpanded(false, 0);
            }

            this.UpdateTouchWindowControlsVisibility();
            this.UpdateTouchWindowControlsPanelLayout(MiniPlayerTouchPanelManager.IsExpanded);
        }

        /// <summary>
        /// Touch optimization: hides or shows the window controls which are normally revealed by
        /// hovering the mini player. They are useless on a touch screen, so they must also not be
        /// clickable by accident.
        /// </summary>
        protected virtual void UpdateTouchWindowControlsVisibility()
        {
        }

        /// <summary>
        /// Touch optimization: shows or hides the panel column which contains the window buttons.
        /// </summary>
        protected virtual void UpdateTouchWindowControlsPanelLayout(bool isExpanded)
        {
        }

        /// <summary>
        /// Width the touch window controls panel adds to the window. The Nano Player lays its buttons
        /// out horizontally, so its panel is wider.
        /// </summary>
        public virtual double TouchWindowControlsPanelWidth
        {
            get { return Core.Base.Constants.MiniPlayerTouchPanelWidth; }
        }

        /// <summary>
        /// Touch optimization: dragging the window is only possible on the player's background (the
        /// area which is not the cover art and not a control). The cover art itself responds to a long
        /// press instead, which opens the context menu.
        /// </summary>
        protected void EnableTouchDragArea(FrameworkElement area, params FrameworkElement[] excluded)
        {
            if (area == null)
            {
                return;
            }

            area.PreviewMouseLeftButtonDown += (sender, e) => this.TouchDragArea_PreviewMouseLeftButtonDown(sender, e, excluded);
        }

        /// <summary>
        /// Touch optimization: the context menu belongs to the cover art only. A right click anywhere
        /// else on the player is ignored, because the rest of the surface drags the window.
        /// </summary>
        protected void LimitContextMenuToTouchTarget(FrameworkElement contextMenuTarget)
        {
            this.ContextMenuOpening += (sender, e) =>
            {
                if (!Dopamine.Utils.TouchOptimization.IsEnabled)
                {
                    return;
                }

                var source = e.OriginalSource as DependencyObject;

                if (contextMenuTarget != null && source != null && contextMenuTarget.IsAncestorOf(source))
                {
                    return;
                }

                e.Handled = true;
            };
        }

        private void TouchDragArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e, FrameworkElement[] excluded)
        {
            if (!Dopamine.Utils.TouchOptimization.IsEnabled)
            {
                return;
            }

            var source = e.OriginalSource as DependencyObject;

            if (source == null || IsInteractiveSource(source))
            {
                return;
            }

            if (excluded != null)
            {
                foreach (FrameworkElement element in excluded)
                {
                    if (element != null && element.IsAncestorOf(source))
                    {
                        return;
                    }
                }
            }

            this.MouseLeftButtonDownHandler(sender, e);
        }

        /// <summary>
        /// True when the element under the finger is (or is inside) a control which handles the press
        /// itself, so dragging the window must not steal it.
        /// </summary>
        private static bool IsInteractiveSource(DependencyObject source)
        {
            DependencyObject current = source;

            while (current != null)
            {
                if (current is System.Windows.Controls.Primitives.ButtonBase ||
                    current is System.Windows.Controls.Slider ||
                    current is System.Windows.Controls.Primitives.TextBoxBase ||
                    current is System.Windows.Controls.Primitives.ScrollBar ||
                    current is System.Windows.Controls.Primitives.Thumb)
                {
                    return true;
                }

                current = System.Windows.Media.VisualTreeHelper.GetParent(current) ?? System.Windows.LogicalTreeHelper.GetParent(current);
            }

            return false;
        }
    }
}
