using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using Dopamine.Core.Base;
using Dopamine.Utils;
using System;
using System.Windows;

namespace Dopamine.Services.Shell
{
    /// <summary>
    /// Handles the touch window controls panel of the mini players. On a touch screen the window
    /// buttons (switch player / minimize / close) can't be revealed by hovering, because there is no
    /// mouse pointer. Instead, they are shown in a panel which is revealed by growing the shell
    /// window to the left: the window's left edge moves left by the panel width and its width grows by
    /// the same amount, so the player content itself stays at the same position on the screen.
    /// Every player keeps its original height (the Nano Player lays its buttons out horizontally).
    /// This is a static class because there is exactly one shell window, and both the mini player
    /// views and the shell service need to agree on the current shift (the shell service must not
    /// save the expanded window position to the settings).
    /// </summary>
    public static class MiniPlayerTouchPanelManager
    {
        private static Window window;
        private static bool isExpanded;

        // Geometry of the window right before expanding, and right after expanding (which can differ
        // from the expected values, because the expanded window is kept inside the virtual screen).
        // They allow to restore the exact previous geometry, also when the window was moved while the
        // panel was expanded.
        private static double baseLeft;
        private static double baseTop;
        private static double baseWidth;
        private static double baseHeight;
        private static double expandedLeft;
        private static double expandedTop;
        private static double expandedPanelWidth;

        /// <summary>Raised on the UI thread when the panel is expanded or collapsed.</summary>
        public static event EventHandler ExpansionChanged = delegate { };

        public static bool IsExpanded
        {
            get { return isExpanded; }
        }

        /// <summary>
        /// Expands or collapses the touch window controls panel. <paramref name="panelWidth"/> is the
        /// width the panel adds to the window (the Nano Player lays its buttons out horizontally, so
        /// its panel is wider). Returns the resulting state: expanding is refused when touch
        /// optimization is disabled or when there is no mini player window to expand.
        /// </summary>
        public static bool SetExpanded(bool expand, double panelWidth)
        {
            if (!expand)
            {
                return Collapse();
            }

            if (!TouchOptimization.IsEnabled)
            {
                return false;
            }

            return Expand(panelWidth);
        }

        /// <summary>
        /// Forgets the expanded state without touching the window geometry. Used by the shell when it
        /// sets the window geometry itself (e.g. when switching between players or player types), so
        /// the new geometry isn't treated as an expanded mini player.
        /// </summary>
        public static void Reset()
        {
            if (!isExpanded)
            {
                return;
            }

            isExpanded = false;
            window = null;
            ExpansionChanged(null, EventArgs.Empty);
        }

        private static bool Expand(double panelWidth)
        {
            if (isExpanded)
            {
                return true;
            }

            Window shellWindow = GetShellWindow();

            if (shellWindow == null)
            {
                return false;
            }

            panelWidth = Math.Max(0, panelWidth);

            baseLeft = shellWindow.Left;
            baseTop = shellWindow.Top;
            baseWidth = shellWindow.Width;
            baseHeight = shellWindow.Height;

            // Only the width grows: the player content keeps its size and position on the screen, and
            // every panel fits in the player's own height (the Nano one is horizontal).
            double newWidth = baseWidth + panelWidth;
            double newHeight = baseHeight;
            double newLeft = baseLeft - panelWidth;
            double newTop = baseTop;

            // Keep the expanded window on the screen: if it can't grow to the left, the player content
            // temporarily moves right instead of the panel ending up off-screen.
            ClampToVirtualScreen(ref newLeft, ref newTop, newWidth, newHeight);

            window = shellWindow;
            expandedLeft = newLeft;
            expandedTop = newTop;
            expandedPanelWidth = panelWidth;
            isExpanded = true;

            // Resize before moving, so the player content ends up at its original position.
            try
            {
                shellWindow.Width = newWidth;
                shellWindow.Height = newHeight;
                shellWindow.Left = newLeft;
                shellWindow.Top = newTop;
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not expand the mini player for the touch window controls. Exception: {0}", ex.Message);
                isExpanded = false;
                window = null;
                return false;
            }

            ExpansionChanged(null, EventArgs.Empty);

            return true;
        }

        private static bool Collapse()
        {
            if (!isExpanded)
            {
                return true;
            }

            Window shellWindow = window;

            isExpanded = false;
            window = null;

            if (shellWindow == null)
            {
                ExpansionChanged(null, EventArgs.Empty);
                return false;
            }

            // Keep a move which was performed while the panel was expanded.
            double movedLeft = shellWindow.Left - expandedLeft;
            double movedTop = shellWindow.Top - expandedTop;

            try
            {
                shellWindow.Width = baseWidth;
                shellWindow.Height = baseHeight;
                shellWindow.Left = baseLeft + movedLeft;
                shellWindow.Top = baseTop + movedTop;
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not collapse the mini player touch window controls. Exception: {0}", ex.Message);
            }

            ExpansionChanged(null, EventArgs.Empty);

            return true;
        }

        private static Window GetShellWindow()
        {
            Window shellWindow = Application.Current == null ? null : Application.Current.MainWindow;

            if (shellWindow == null || !shellWindow.IsVisible || shellWindow.WindowState != WindowState.Normal)
            {
                return null;
            }

            return shellWindow;
        }

        private static void ClampToVirtualScreen(ref double left, ref double top, double width, double height)
        {
            try
            {
                // SystemParameters is expressed in the same device independent units as Window.Left/Top.
                double virtualLeft = SystemParameters.VirtualScreenLeft;
                double virtualTop = SystemParameters.VirtualScreenTop;
                double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
                double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

                if (left < virtualLeft)
                {
                    left = virtualLeft;
                }

                if (top < virtualTop)
                {
                    top = virtualTop;
                }

                if (left + width > virtualRight)
                {
                    left = virtualRight - width;
                }

                if (top + height > virtualBottom)
                {
                    top = virtualBottom - height;
                }
            }
            catch
            {
                // Never let a screen geometry problem prevent the panel from being shown.
            }
        }

        /// <summary>
        /// Returns the window left which corresponds to the given (expanded) window left, i.e. the
        /// position to save to the settings: the position the window would have when the panel is
        /// collapsed. This keeps the window position restore intact.
        /// </summary>
        public static double GetSavedLeft(double currentLeft)
        {
            return isExpanded ? baseLeft + (currentLeft - expandedLeft) : currentLeft;
        }

        /// <summary>
        /// Returns the window top which corresponds to the given (expanded) window top, i.e. the
        /// position to save to the settings.
        /// </summary>
        public static double GetSavedTop(double currentTop)
        {
            return isExpanded ? baseTop + (currentTop - expandedTop) : currentTop;
        }

        /// <summary>Left position of the player content (i.e. excluding the touch panel).</summary>
        public static double GetContentLeft(double windowLeft)
        {
            return isExpanded ? windowLeft + expandedPanelWidth : windowLeft;
        }

        /// <summary>Width of the player content (i.e. excluding the touch panel).</summary>
        public static double GetContentWidth(double windowWidth)
        {
            return isExpanded ? windowWidth - expandedPanelWidth : windowWidth;
        }

        /// <summary>
        /// Height of the player content. The panel never changes the window height (the Nano Player
        /// lays its buttons out horizontally), so this is the window height.
        /// </summary>
        public static double GetContentHeight(double windowHeight)
        {
            return windowHeight;
        }
    }
}
