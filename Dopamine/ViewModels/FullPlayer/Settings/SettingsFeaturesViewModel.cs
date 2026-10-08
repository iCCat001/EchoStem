using Digimezzo.Foundation.Core.Settings;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Core.Base;
using Dopamine.Services.Dialog;
using Dopamine.Views.Common;
using Prism.Events;
using Prism.Mvvm;

namespace Dopamine.ViewModels.FullPlayer.Settings
{
    public class SettingsFeaturesViewModel : BindableBase
    {
        private IDialogService dialogService;
        private bool checkBoxNonPublicServicesChecked;
        private bool checkBoxTouchOptimizationChecked;

        public bool CheckBoxNonPublicServicesChecked
        {
            get { return this.checkBoxNonPublicServicesChecked; }
            set
            {
                if (value == this.checkBoxNonPublicServicesChecked)
                {
                    return;
                }

                if (!value)
                {
                    // Disabling never requires a password.
                    SettingsClient.Set<bool>("Features", "NonPublicExperimentalServices", false, true);
                    SetProperty<bool>(ref this.checkBoxNonPublicServicesChecked, false);
                    return;
                }

                // Enabling the non-public, experimental features requires the password.
                if (this.PromptForPassword())
                {
                    SettingsClient.Set<bool>("Features", "NonPublicExperimentalServices", true, true);
                    SetProperty<bool>(ref this.checkBoxNonPublicServicesChecked, true);
                }
                else
                {
                    // Wrong password, or cancelled: keep the switch off and force the UI to reflect it.
                    SetProperty<bool>(ref this.checkBoxNonPublicServicesChecked, false);
                    RaisePropertyChanged(nameof(this.CheckBoxNonPublicServicesChecked));
                }
            }
        }

        public SettingsFeaturesViewModel(IEventAggregator eventAggregator, IDialogService dialogService)
        {
            this.dialogService = dialogService;
            this.checkBoxNonPublicServicesChecked = SettingsClient.Get<bool>("Features", "NonPublicExperimentalServices");
            this.checkBoxTouchOptimizationChecked = SettingsClient.Get<bool>("Features", "TouchOptimization");
        }

        public bool CheckBoxTouchOptimizationChecked
        {
            get { return this.checkBoxTouchOptimizationChecked; }
            set
            {
                SettingsClient.Set<bool>("Features", "TouchOptimization", value, true);
                SetProperty<bool>(ref this.checkBoxTouchOptimizationChecked, value);
            }
        }

        private bool PromptForPassword()
        {
            var passwordDialog = new PasswordDialog();

            bool accepted = this.dialogService.ShowCustomDialog(
                0xe72e,
                16,
                ResourceUtils.GetString("Language_Non_Public_Experimental_Features"),
                passwordDialog,
                350,
                0,
                false,
                true,
                true,
                true,
                ResourceUtils.GetString("Language_Ok"),
                ResourceUtils.GetString("Language_Cancel"),
                null);

            return accepted && AccessControl.Verify(passwordDialog.Password);
        }
    }
}
