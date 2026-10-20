using Digimezzo.Foundation.Core.IO;
using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Packaging;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Core.Base;
using Dopamine.Services.Dialog;
using Dopamine.Services.Update;
using Dopamine.Views.FullPlayer.Information;
using Prism.Commands;
using Prism.Mvvm;
using System;
using Prism.Ioc;
using Dopamine.Core.Extensions;

namespace Dopamine.ViewModels.FullPlayer.Information
{
    public class InformationAboutViewModel : BindableBase
    {
        private IContainerProvider container;
        private IDialogService dialogService;
        private IUpdateChecker updateChecker;
        private Package package;

        public DelegateCommand ShowLicenseCommand { get; set; }
        public DelegateCommand CheckForUpdatesCommand { get; set; }
        public DelegateCommand<string> OpenLinkCommand { get; set; }

        public Package Package
        {
            get { return this.package; }
            set { SetProperty<Package>(ref this.package, value); }
        }

        public ExternalComponent[] Components => ProductInformation.Components;

        public string Copyright => ProductInformation.Copyright;

        public string UpdateDate
        {
            get
            {
                try
                {
                    string location = this.GetType().Assembly.Location;

                    if (!string.IsNullOrEmpty(location) && System.IO.File.Exists(location))
                    {
                        return System.IO.File.GetLastWriteTime(location).ToString("yyyy-MM-dd");
                    }
                }
                catch (Exception ex)
                {
                    LogClient.Error("Could not determine the application update date. Exception: {0}", ex.Message);
                }

                return string.Empty;
            }
        }

        public string DonateUrl => ContactInformation.DonateLink;

        public string FeedbackUrl => "https://github.com/iCCat001/EchoStem/issues";

        public InformationAboutViewModel(IContainerProvider container, IDialogService dialogService, IUpdateChecker updateChecker)
        {
            this.container = container;
            this.dialogService = dialogService;
            this.updateChecker = updateChecker;
            this.Package = new Package(ProcessExecutable.Name(), ProcessExecutable.AssemblyVersion());

            this.CheckForUpdatesCommand = new DelegateCommand(async () => await this.updateChecker.CheckForUpdatesAsync(silent: false));

            this.ShowLicenseCommand = new DelegateCommand(() =>
            {
                var view = this.container.Resolve<InformationAboutLicense>();

                this.dialogService.ShowCustomDialog(
                    0xe73e,
                    16,
                    ResourceUtils.GetString("Language_License"),
                    view,
                    400,
                    0,
                    false,
                    true,
                    true,
                    false,
                    ResourceUtils.GetString("Language_Ok"),
                    string.Empty,
                    null);
            });

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
    }
}
