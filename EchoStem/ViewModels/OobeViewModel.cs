using Digimezzo.Foundation.Core.IO;
using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Utils;
using EchoStem.Core.Enums;
using EchoStem.Services.Dialog;
using EchoStem.Services.Migration;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace EchoStem.ViewModels
{
    public class OobeViewModel : BindableBase
    {
        private readonly IApplicationDataMigrationService migrationService;

        private bool isOverlayVisible;
        private bool showButtonGoBack;
        private bool showButtonFinish;
        private bool isMigrating;
        private bool hasMigrationError;
        private string migrationErrorMessage;

        private OobePage selectedOobePage;

        public DelegateCommand GoBackCommand { get; set; }
        public DelegateCommand GoForwardCommand { get; set; }
        public DelegateCommand<string> OpenLinkCommand { get; set; }
        public DelegateCommand MigrateCommand { get; set; }
        public DelegateCommand SkipMigrationCommand { get; set; }

        /// <summary>True when old Dopamine data was detected: the first wizard page offers the migration.</summary>
        public bool MigrationAvailable { get; }

        public bool IsMigrating
        {
            get { return this.isMigrating; }
            private set
            {
                if (SetProperty<bool>(ref this.isMigrating, value))
                {
                    RaisePropertyChanged(nameof(this.IsNotMigrating));
                }
            }
        }

        public bool IsNotMigrating => !this.isMigrating;

        public bool HasMigrationError
        {
            get { return this.hasMigrationError; }
            private set { SetProperty<bool>(ref this.hasMigrationError, value); }
        }

        public string MigrationErrorMessage
        {
            get { return this.migrationErrorMessage; }
            private set { SetProperty<string>(ref this.migrationErrorMessage, value); }
        }

        public bool IsOverlayVisible
        {
            get { return this.isOverlayVisible; }
            set { SetProperty<bool>(ref this.isOverlayVisible, value); }
        }

        public bool ShowButtonGoBack
        {
            get { return this.showButtonGoBack; }
            set { SetProperty<bool>(ref this.showButtonGoBack, value); }
        }

        public bool ShowButtonFinish
        {
            get { return this.showButtonFinish; }
            set { SetProperty<bool>(ref this.showButtonFinish, value); }
        }

        public OobePage SelectedOobePage
        {
            get { return (OobePage)this.selectedOobePage; }
            set
            {
                // The migration page only exists when old data was found. Any attempt to navigate to
                // it otherwise (e.g. swiping the pivot) is redirected to the welcome page.
                OobePage page = value;

                if (page == OobePage.Migration && !this.MigrationAvailable)
                {
                    page = OobePage.Welcome;
                }

                if (SetProperty<OobePage>(ref this.selectedOobePage, page))
                {
                    RaisePropertyChanged(nameof(this.CanGoBack));
                    RaisePropertyChanged(nameof(this.CanFinish));
                }
            }
        }

        public bool CanGoBack
        {
            get
            {
                if (this.selectedOobePage.Equals(OobePage.Migration) ||
                    this.selectedOobePage.Equals(OobePage.Welcome) ||
                    this.selectedOobePage.Equals(OobePage.Language))
                {
                    return false;
                }

                return true;
            }
        }

        public bool CanFinish
        {
            get
            {
                if (this.selectedOobePage.Equals(OobePage.Finish))
                {
                    return true;
                }

                return false;
            }
        }

        public OobeViewModel(IDialogService dialogService, IApplicationDataMigrationService migrationService)
        {
            this.migrationService = migrationService;
            this.MigrationAvailable = migrationService.IsMigrationAvailable;

            // Start on the migration page when there is data to bring over, otherwise on the welcome page.
            this.selectedOobePage = this.MigrationAvailable ? OobePage.Migration : OobePage.Welcome;

            LogClient.Info("OOBE: migration available={0}, starting page={1}", this.MigrationAvailable, this.selectedOobePage);

            dialogService.DialogVisibleChanged += (isDialogVisible) => { this.IsOverlayVisible = isDialogVisible; };

            this.GoBackCommand = new DelegateCommand(() => this.GoBack());
            this.GoForwardCommand = new DelegateCommand(() => this.GoForward());
            this.MigrateCommand = new DelegateCommand(() => this.MigrateAsync());
            this.SkipMigrationCommand = new DelegateCommand(() => this.SkipMigration());

            this.OpenLinkCommand = new DelegateCommand<string>((url) =>
            {
                try
                {
                    Actions.TryOpenLink(url);
                }
                catch (Exception ex)
                {
                    LogClient.Error("Could not open link {0}. Exception: {1}", url, ex.Message);
                }
            });
        }

        private void MigrateAsync()
        {
            if (this.IsMigrating)
            {
                return;
            }

            // The copy itself runs after the wizard has closed (see App.InitializeShell): closing the
            // wizard writes this session's settings, which must not overwrite the migrated files.
            LogClient.Info("Migration requested by the user");
            this.IsMigrating = true;
            ApplicationDataMigrationService.RequestMigration();

            Window oobeWindow = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(window => window.DataContext == this);

            if (oobeWindow != null)
            {
                oobeWindow.Close();
            }
        }

        private void SkipMigration()
        {
            this.migrationService.MarkAsHandled();
            this.SelectedOobePage = OobePage.Welcome;
        }


        private void GoBack()
        {
            switch (this.selectedOobePage)
            {
                case OobePage.Migration:
                case OobePage.Welcome:
                    // Do nothing
                    break;
                case OobePage.Language:
                    this.SelectedOobePage = OobePage.Welcome;
                    break;
                case OobePage.Appearance:
                    this.SelectedOobePage = OobePage.Language;
                    break;
                case OobePage.Collection:
                    this.SelectedOobePage = OobePage.Appearance;
                    break;
                case OobePage.Donate:
                    this.SelectedOobePage = OobePage.Collection;
                    break;
                case OobePage.Finish:
                    this.SelectedOobePage = OobePage.Donate;
                    break;
                default:
                    break;
            }
        }

        private void GoForward()
        {
            switch (this.selectedOobePage)
            {
                case OobePage.Migration:
                    this.SelectedOobePage = OobePage.Welcome;
                    break;
                case OobePage.Welcome:
                    this.SelectedOobePage = OobePage.Language;
                    break;
                case OobePage.Language:
                    this.SelectedOobePage = OobePage.Appearance;
                    break;
                case OobePage.Appearance:
                    this.SelectedOobePage = OobePage.Collection;
                    break;
                case OobePage.Collection:
                    this.SelectedOobePage = OobePage.Donate;
                    break;
                case OobePage.Donate:
                    this.SelectedOobePage = OobePage.Finish;
                    break;
                case OobePage.Finish:
                    // Do nothing
                    break;
                default:
                    break;
            }
        }
    }
}
