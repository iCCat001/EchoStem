using Digimezzo.Foundation.Core.Settings;
using Prism.Mvvm;
using System.Threading.Tasks;

namespace Dopamine.ViewModels.FullPlayer.Settings
{
    public class SettingsStartupViewModel : BindableBase
    {
        private bool checkBoxCheckForUpdatesAtStartupChecked;
        private bool checkBoxInstallUpdatesAutomaticallyChecked;
        private bool checkBoxStartupPageChecked;
        private bool checkBoxRememberLastPlayedTrackChecked;
        private bool isportable;

        public bool CheckBoxCheckForUpdatesAtStartupChecked
        {
            get { return this.checkBoxCheckForUpdatesAtStartupChecked; }
            set
            {
                SettingsClient.Set<bool>("Updates", "CheckAtStartup", value);
                SetProperty<bool>(ref this.checkBoxCheckForUpdatesAtStartupChecked, value);
            }
        }

        public bool CheckBoxInstallUpdatesAutomaticallyChecked
        {
            get { return this.checkBoxInstallUpdatesAutomaticallyChecked; }
            set
            {
                SettingsClient.Set<bool>("Updates", "AutomaticDownload", value);
                SetProperty<bool>(ref this.checkBoxInstallUpdatesAutomaticallyChecked, value);
            }
        }

        public bool CheckBoxStartupPageChecked
        {
            get { return this.checkBoxStartupPageChecked; }
            set
            {
                SettingsClient.Set<bool>("Startup", "ShowLastSelectedPage", value);
                SetProperty<bool>(ref this.checkBoxStartupPageChecked, value);
            }
        }

        public bool CheckBoxRememberLastPlayedTrackChecked
        {
            get { return this.checkBoxRememberLastPlayedTrackChecked; }
            set
            {
                SettingsClient.Set<bool>("Startup", "RememberLastPlayedTrack", value);
                SetProperty<bool>(ref this.checkBoxRememberLastPlayedTrackChecked, value);
            }
        }

        public bool IsPortable
        {
            get { return this.isportable; }
            set { SetProperty<bool>(ref this.isportable, value); }
        }

        public SettingsStartupViewModel()
        {
            this.IsPortable = SettingsClient.Get<bool>("Configuration", "IsPortable");

            // CheckBoxes
            this.GetCheckBoxesAsync();

            // No automatic updates in the portable version
            if (!this.IsPortable)
            {
                this.checkBoxInstallUpdatesAutomaticallyChecked = SettingsClient.Get<bool>("Updates", "AutomaticDownload");
            }
            else
            {
                this.checkBoxInstallUpdatesAutomaticallyChecked = false;
            }
        }

        private async void GetCheckBoxesAsync()
        {
            await Task.Run(() =>
            {
                this.checkBoxCheckForUpdatesAtStartupChecked = SettingsClient.Get<bool>("Updates", "CheckAtStartup");
                this.checkBoxStartupPageChecked = SettingsClient.Get<bool>("Startup", "ShowLastSelectedPage");
                this.checkBoxRememberLastPlayedTrackChecked = SettingsClient.Get<bool>("Startup", "RememberLastPlayedTrack");
            });
        }
    }
}
