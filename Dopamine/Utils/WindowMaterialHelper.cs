using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using MicaWPF.Core.Enums;
using MicaWPF.Core.Extensions;
using MicaWPF.Core.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Dopamine.Utils
{
    /// <summary>
    /// The visual material of the window background.
    /// </summary>
    public enum WindowMaterial
    {
        /// <summary>The original opaque, theme-colored background.</summary>
        Traditional,

        /// <summary>Windows 11 Mica (the desktop wallpaper tint).</summary>
        Mica,

        /// <summary>Windows 11 Acrylic, or the legacy acrylic blur on Windows 10.</summary>
        Acrylic
    }

    /// <summary>
    /// Applies a window background material (Traditional / Mica / Acrylic) to a WPF window.
    /// </summary>
    public static class WindowMaterialHelper
    {
        private const string SettingNamespace = "Appearance";
        private const string SettingName = "WindowMaterial";

        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMSBT_NONE = 1;
        private const int DWMSBT_MAINWINDOW = 2;        // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3;   // Acrylic

        // Windows 10 legacy blur (SetWindowCompositionAttribute).
        private const int WCA_ACCENT_POLICY = 19;
        private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;
        private const int ACCENT_DISABLED = 0;

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x00080000;

        // Opacity of the translucent list panes when a material is active.
        private const double PaneBackgroundOpacity = 0.5;

        // Tint opacity of the Windows 10 legacy acrylic. 0.8 (80%) is the tint opacity that
        // Microsoft uses for the default Fluent acrylic, and it keeps text legible on top of it.
        private const double AcrylicTintOpacity = 0.8;

        private const int WM_SIZE = 0x0005;
        private const int WM_WINDOWPOSCHANGED = 0x0047;
        private const int WM_DWMCOMPOSITIONCHANGED = 0x031A;

        private static WindowMaterial currentMaterial = WindowMaterial.Traditional;
        private static Window currentWindow;
        private static bool backdropSuppressed;
        private static readonly System.Collections.Generic.HashSet<IntPtr> attachedWindows = new System.Collections.Generic.HashSet<IntPtr>();

        public static bool IsMicaSupported => OsHelper.IsWindows11_OrGreater;

        public static WindowMaterial GetSavedMaterial()
        {
            try
            {
                string value = SettingsClient.Get<string>(SettingNamespace, SettingName);

                if (Enum.TryParse(value, out WindowMaterial material))
                {
                    return material;
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not read the window material setting. Exception: {0}", ex.Message);
            }

            return WindowMaterial.Traditional;
        }

        /// <summary>
        /// Gets the materials that can be used on the current OS.
        /// </summary>
        public static WindowMaterial[] GetSupportedMaterials()
        {
            return IsMicaSupported
                ? new[] { WindowMaterial.Traditional, WindowMaterial.Mica, WindowMaterial.Acrylic }
                : new[] { WindowMaterial.Traditional, WindowMaterial.Acrylic };
        }

        private static Brush CreatePaneBackgroundBrush()
        {
            var baseBrush = Application.Current?.TryFindResource("Brush_PaneBackground") as SolidColorBrush;
            Color baseColor = baseBrush != null ? baseBrush.Color : Colors.Black;
            byte alpha = (byte)Math.Round(PaneBackgroundOpacity * 255);
            return new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        }

        /// <summary>
        /// Applies the given material to the given window.
        /// </summary>
        public static void Apply(Window window, WindowMaterial material)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                // Mica is only available on Windows 11.
                if (material == WindowMaterial.Mica && !IsMicaSupported)
                {
                    material = WindowMaterial.Traditional;
                }

                currentMaterial = material;
                currentWindow = window;

                IntPtr handle = new WindowInteropHelper(window).Handle;

                // Reset any window-level background overrides (fall back to the theme brushes).
                window.Resources.Remove("Brush_MainWindowBackground");
                window.Resources.Remove("Brush_WindowHeaderBackground");
                window.Resources.Remove("Brush_PaneBackground");
                window.Resources.Remove("Brush_AlbumTileInfoBackground");

                if (material == WindowMaterial.Traditional)
                {
                    RemoveBackdrop(handle);
                    ShowSystemWindowButtons(handle);
                    SetCompositionBackground(window, GetWindowBackgroundColor());
                    return;
                }

                // The backdrop is only visible where the window content is transparent.
                window.Resources["Brush_MainWindowBackground"] = Brushes.Transparent;
                window.Resources["Brush_WindowHeaderBackground"] = Brushes.Transparent;

                // The list panes (artists list, playlists panel, albums detail pane) become
                // translucent so the material shows through them.
                window.Resources["Brush_PaneBackground"] = CreatePaneBackgroundBrush();

                // The album tile info panel (title/artist under each cover) keeps its opaque grey
                // background by default; make it translucent so it matches the list/artist
                // sidebars and the material shows through it.
                window.Resources["Brush_AlbumTileInfoBackground"] = CreatePaneBackgroundBrush();

                // Extending the frame brings back the system caption buttons; hide them
                // (Dopamine draws its own window controls).
                HideSystemWindowButtons(handle);

                if (backdropSuppressed)
                {
                    // The page which is currently shown covers the whole window (Now Playing
                    // draws its own album art background): don't let DWM render the backdrop.
                    ApplySuppressedSurface(window, handle);
                }
                else if (material == WindowMaterial.Mica)
                {
                    window.EnableBackdrop(BackdropType.Mica);
                    ExtendFrameIntoClientArea(handle);
                }
                else if (OsHelper.IsWindows11_OrGreater)
                {
                    window.EnableBackdrop(BackdropType.Acrylic);
                    ExtendFrameIntoClientArea(handle);
                }
                else
                {
                    // Windows 10: MicaWPF does not provide acrylic, use the legacy blur.
                    EnableWindows10Acrylic(handle);
                }

                // MicaWPF forces the backdrop's dark/light variant from ITS OWN theme, which
                // overrides ours, so apply the application theme afterwards so it wins.
                ApplyDarkMode(handle);

                // Let the DWM backdrop composite through the WPF surface.
                SetCompositionBackground(window, Color.FromArgb(0, 0, 0, 0));
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not apply the '{0}' window material. Exception: {1}", material, ex.Message);
            }
        }

        /// <summary>
        /// Re-applies the material whenever the window is resized/moved or DWM composition changes.
        /// This keeps the backdrop present during the OS minimize/maximize/restore animations.
        /// </summary>
        public static void Attach(Window window)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero)
                {
                    return;
                }

                lock (attachedWindows)
                {
                    if (!attachedWindows.Add(handle))
                    {
                        return;
                    }
                }

                HwndSource source = HwndSource.FromHwnd(handle);
                source?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (msg == WM_SIZE || msg == WM_WINDOWPOSCHANGED || msg == WM_DWMCOMPOSITIONCHANGED)
                    {
                        Refresh(window);
                    }

                    return IntPtr.Zero;
                });
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not attach the window material hook. Exception: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Re-applies only the DWM backdrop/frame for the currently saved material, without
        /// touching the brushes or re-registering with MicaWPF. Used repeatedly during window
        /// state transitions (minimize/restore animations), where DWM resets the backdrop.
        /// </summary>
        public static void Refresh(Window window)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                WindowMaterial material = currentMaterial;

                if (material == WindowMaterial.Traditional)
                {
                    return;
                }

                if (material == WindowMaterial.Mica && !IsMicaSupported)
                {
                    return;
                }

                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero)
                {
                    return;
                }

                HideSystemWindowButtons(handle);

                if (backdropSuppressed)
                {
                    ApplySuppressedSurface(window, handle);
                }
                else if (material == WindowMaterial.Mica)
                {
                    int mica = DWMSBT_MAINWINDOW;
                    DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int));
                    ExtendFrameIntoClientArea(handle);
                }
                else if (OsHelper.IsWindows11_OrGreater)
                {
                    int acrylic = DWMSBT_TRANSIENTWINDOW;
                    DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref acrylic, sizeof(int));
                    ExtendFrameIntoClientArea(handle);
                }
                else
                {
                    EnableWindows10Acrylic(handle);
                }

                ApplyDarkMode(handle);

                if (!backdropSuppressed)
                {
                    SetCompositionBackground(window, Color.FromArgb(0, 0, 0, 0));
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not refresh the window material. Exception: {0}", ex.Message);
            }
        }

        private static void RemoveBackdrop(IntPtr handle)
        {
            if (handle == IntPtr.Zero || !OsHelper.IsWindows11_OrGreater)
            {
                return;
            }

            int none = DWMSBT_NONE;
            DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));

            var margins = new MARGINS();
            DwmExtendFrameIntoClientArea(handle, ref margins);
        }

        /// <summary>
        /// Suppresses (or re-enables) the DWM backdrop without changing the window resources.
        /// Used while a page which covers the whole window (the Now Playing page) is shown: its
        /// background makes the backdrop invisible, so having DWM render it is wasted work.
        /// </summary>
        public static void SetBackdropSuppressed(bool suppressed)
        {
            if (backdropSuppressed == suppressed)
            {
                return;
            }

            backdropSuppressed = suppressed;

            LogClient.Info("Window backdrop {0}.", suppressed ? "suppressed" : "restored");

            Window window = currentWindow;

            if (window == null)
            {
                return;
            }

            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;

                if (handle == IntPtr.Zero)
                {
                    return;
                }

                if (suppressed)
                {
                    ApplySuppressedSurface(window, handle);
                }
                else
                {
                    // Re-apply the material completely: brushes, frame extension and backdrop.
                    Apply(window, currentMaterial);
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not change the window backdrop suppression. Exception: {0}", ex.Message);
            }
        }

        // Removes only the backdrop, keeping the extended frame (and thus the transparent surface)
        // as it is, so the window chrome does not change.
        private static void ApplyNoBackdrop(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            if (OsHelper.IsWindows11_OrGreater)
            {
                int none = DWMSBT_NONE;
                DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));
            }
            else
            {
                DisableWindows10Acrylic(handle);
            }
        }

        // Removes the backdrop, but keeps the extended frame (which gives the window its shadow,
        // rounded corners and edge line). The window surface is filled with the theme background
        // color, so the transparent parts of the page (visible during its transitions) don't show
        // the desktop through the window.
        private static void ApplySuppressedSurface(Window window, IntPtr handle)
        {
            ApplyNoBackdrop(handle);

            if (OsHelper.IsWindows11_OrGreater)
            {
                ExtendFrameIntoClientArea(handle);
            }

            SetCompositionBackground(window, GetWindowBackgroundColor());
        }

        private static void SetCompositionBackground(Window window, Color color)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);

                if (source?.CompositionTarget != null)
                {
                    source.CompositionTarget.BackgroundColor = color;
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not set the window background color. Exception: {0}", ex.Message);
            }
        }

        private static Color GetWindowBackgroundColor()
        {
            try
            {
                if (Application.Current?.TryFindResource("Brush_MainWindowBackground") is SolidColorBrush brush)
                {
                    return brush.Color;
                }
            }
            catch
            {
            }

            return Colors.Black;
        }

        private static void ExtendFrameIntoClientArea(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(handle, ref margins);
        }

        private static void ApplyDarkMode(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                bool useLightTheme = SettingsClient.Get<bool>("Appearance", "EnableLightTheme");
                int darkMode = useLightTheme ? 0 : 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not apply the DWM dark mode. Exception: {0}", ex.Message);
            }
        }

        private static void HideSystemWindowButtons(IntPtr handle)
        {
            int style = GetWindowLong(handle, GWL_STYLE);
            SetWindowLong(handle, GWL_STYLE, style & ~WS_SYSMENU);
        }

        private static void ShowSystemWindowButtons(IntPtr handle)
        {
            int style = GetWindowLong(handle, GWL_STYLE);
            SetWindowLong(handle, GWL_STYLE, style | WS_SYSMENU);
        }

        /// <summary>
        /// Gets the AABBGGRR tint color of the Windows 10 legacy acrylic. The tint follows the
        /// app theme (a light tint on the light theme, a dark one on the dark theme) so text
        /// keeps enough contrast on top of the material.
        /// </summary>
        private static int GetAcrylicTintColor()
        {
            byte alpha = (byte)Math.Round(AcrylicTintOpacity * 255);
            uint rgb = 0x000000; // Dark theme: black tint.

            try
            {
                if (SettingsClient.Get<bool>("Appearance", "EnableLightTheme"))
                {
                    rgb = 0xFFFFFF; // Light theme: white tint.
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not read the light theme setting. Exception: {0}", ex.Message);
            }

            return unchecked((int)(((uint)alpha << 24) | rgb));
        }

        private static void EnableWindows10Acrylic(IntPtr handle)
        {
            SetWindows10Accent(handle, ACCENT_ENABLE_ACRYLICBLURBEHIND, 0x20, GetAcrylicTintColor());
        }

        private static void DisableWindows10Acrylic(IntPtr handle)
        {
            SetWindows10Accent(handle, ACCENT_DISABLED, 0, 0);
        }

        private static void SetWindows10Accent(IntPtr handle, int accentState, int accentFlags, int gradientColor)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var accent = new AccentPolicy
            {
                AccentState = accentState,
                AccentFlags = accentFlags,
                GradientColor = gradientColor // AABBGGRR
            };

            int accentSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);

            try
            {
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WCA_ACCENT_POLICY,
                    SizeOfData = accentSize,
                    Data = accentPtr
                };

                SetWindowCompositionAttribute(handle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    }
}
