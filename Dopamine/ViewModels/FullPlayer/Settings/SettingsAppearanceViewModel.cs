using Digimezzo.Foundation.Core.IO;
using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Core.Base;
using Dopamine.Core.IO;
using Dopamine.Services.Playback;
using Dopamine.Utils;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dopamine.ViewModels.FullPlayer.Settings
{
    /// <summary>
    /// A selectable window material option (with a localized display name).
    /// </summary>
    public sealed class WindowMaterialOption
    {
        public WindowMaterial Value { get; set; }

        public string Display { get; set; }

        public override string ToString()
        {
            return this.Display;
        }
    }

    public class SettingsAppearanceViewModel : BindableBase
    {
        private IPlaybackService playbackService;
        private bool checkBoxCheckBoxShowWindowBorderChecked;
        private bool checkBoxEnableTransparencyChecked;
        private WindowMaterialOption selectedWindowMaterialOption;
        private int nowPlayingBackgroundOpacity;
        private bool enableNowPlayingGradient;
        private bool enableNowPlayingMaterialSync;
        private IEventAggregator eventAggregator;

        public DelegateCommand<string> OpenColorSchemesDirectoryCommand { get; set; }

        public string ColorSchemesDirectory { get; set; }

        public bool IsWindows10 => Constants.IsWindows10;

        public IList<WindowMaterialOption> WindowMaterialOptions { get; }

        public WindowMaterialOption SelectedWindowMaterialOption
        {
            get { return this.selectedWindowMaterialOption; }
            set
            {
                if (value != null)
                {
                    SettingsClient.Set<string>("Appearance", "WindowMaterial", value.Value.ToString(), true);
                }

                SetProperty(ref this.selectedWindowMaterialOption, value);

                this.RaisePropertyChanged(nameof(this.IsMaterialSyncAvailable));

                // The material sync is only available for Mica and Acrylic.
                if (!this.IsMaterialSyncAvailable)
                {
                    this.EnableNowPlayingMaterialSync = false;
                }
            }
        }

        public int NowPlayingBackgroundOpacity
        {
            get { return this.nowPlayingBackgroundOpacity; }
            set
            {
                SettingsClient.Set<int>("Appearance", "NowPlayingBackgroundOpacity", value, true);
                SetProperty<int>(ref this.nowPlayingBackgroundOpacity, value);
            }
        }

        public bool EnableNowPlayingGradient
        {
            get { return this.enableNowPlayingGradient; }
            set
            {
                SettingsClient.Set<bool>("Appearance", "NowPlayingGradient", value, true);
                SetProperty<bool>(ref this.enableNowPlayingGradient, value);
            }
        }

        public bool EnableNowPlayingMaterialSync
        {
            get { return this.enableNowPlayingMaterialSync; }
            set
            {
                SettingsClient.Set<bool>("Appearance", "NowPlayingMaterialSync", value, true);
                SetProperty<bool>(ref this.enableNowPlayingMaterialSync, value);
                this.RaisePropertyChanged(nameof(this.IsMaskOpacityEnabled));
            }
        }

        /// <summary>
        /// The material sync is only available when a Mica or Acrylic material is selected.
        /// </summary>
        public bool IsMaterialSyncAvailable
        {
            get
            {
                return this.selectedWindowMaterialOption != null &&
                       this.selectedWindowMaterialOption.Value != WindowMaterial.Traditional;
            }
        }

        /// <summary>
        /// The mask opacity slider only applies when the material sync is disabled.
        /// </summary>
        public bool IsMaskOpacityEnabled
        {
            get { return !this.enableNowPlayingMaterialSync; }
        }

        public bool CheckBoxCheckBoxShowWindowBorderChecked
        {
            get { return this.checkBoxCheckBoxShowWindowBorderChecked; }
            set
            {
                SettingsClient.Set<bool>("Appearance", "ShowWindowBorder", value, true);
                SetProperty<bool>(ref this.checkBoxCheckBoxShowWindowBorderChecked, value);
            }
        }

        public bool CheckBoxEnableTransparencyChecked
        {
            get { return this.checkBoxEnableTransparencyChecked; }
            set
            {
                SettingsClient.Set<bool>("Appearance", "EnableTransparency", value);
                SetProperty<bool>(ref this.checkBoxEnableTransparencyChecked, value);
            }
        }

        public SettingsAppearanceViewModel(IPlaybackService playbackService, IEventAggregator eventAggregator)
        {
            this.playbackService = playbackService;
            this.eventAggregator = eventAggregator;

            // Window material options (Mica is only available on Windows 11).
            this.WindowMaterialOptions = new List<WindowMaterialOption>();

            foreach (WindowMaterial material in WindowMaterialHelper.GetSupportedMaterials())
            {
                this.WindowMaterialOptions.Add(new WindowMaterialOption
                {
                    Value = material,
                    Display = this.GetWindowMaterialDisplay(material)
                });
            }

            WindowMaterial savedMaterial = WindowMaterialHelper.GetSavedMaterial();

            foreach (WindowMaterialOption option in this.WindowMaterialOptions)
            {
                if (option.Value == savedMaterial)
                {
                    this.selectedWindowMaterialOption = option;
                    break;
                }
            }

            if (this.selectedWindowMaterialOption == null && this.WindowMaterialOptions.Count > 0)
            {
                this.selectedWindowMaterialOption = this.WindowMaterialOptions[0];
            }

            this.ColorSchemesDirectory = System.IO.Path.Combine(SettingsClient.ApplicationFolder(), ApplicationPaths.ColorSchemesFolder);

            try
            {
                this.nowPlayingBackgroundOpacity = SettingsClient.Get<int>("Appearance", "NowPlayingBackgroundOpacity");
                this.enableNowPlayingGradient = SettingsClient.Get<bool>("Appearance", "NowPlayingGradient");
                this.enableNowPlayingMaterialSync = SettingsClient.Get<bool>("Appearance", "NowPlayingMaterialSync");
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not read the Now Playing background settings. Exception: {0}", ex.Message);
                this.nowPlayingBackgroundOpacity = 88;
                this.enableNowPlayingGradient = true;
                this.enableNowPlayingMaterialSync = false;
            }

            // The material sync can't be active for the default material.
            if (!this.IsMaterialSyncAvailable)
            {
                this.enableNowPlayingMaterialSync = false;
            }

            this.OpenColorSchemesDirectoryCommand = new DelegateCommand<string>((colorSchemesDirectory) =>
            {
                try
                {
                    Actions.TryOpenPath(colorSchemesDirectory);
                }
                catch (Exception ex)
                {
                    LogClient.Error("Could not open the ColorSchemes directory. Exception: {0}", ex.Message);
                }
            });

            this.GetCheckBoxesAsync();
        }

        public async void GetCheckBoxesAsync()
        {
            await Task.Run(() =>
            {
                this.checkBoxCheckBoxShowWindowBorderChecked = SettingsClient.Get<bool>("Appearance", "ShowWindowBorder");
                this.checkBoxEnableTransparencyChecked = SettingsClient.Get<bool>("Appearance", "EnableTransparency");
            });
        }

        private string GetWindowMaterialDisplay(WindowMaterial material)
        {
            switch (material)
            {
                case WindowMaterial.Mica:
                    return ResourceUtils.GetString("Language_Window_Material_Mica");

                case WindowMaterial.Acrylic:
                    return ResourceUtils.GetString("Language_Window_Material_Acrylic");

                default:
                    return ResourceUtils.GetString("Language_Window_Material_Traditional");
            }
        }
    }
}
