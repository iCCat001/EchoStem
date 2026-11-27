using Digimezzo.Foundation.Core.IO;
using Digimezzo.Foundation.Core.Logging;
using Prism.Commands;
using Prism.Mvvm;
using System;

namespace EchoStem.ViewModels.FullPlayer.Information
{
    public class InformationAboutLicenseViewModel : BindableBase
    {
        public DelegateCommand<string> OpenLinkCommand { get; set; }

        public InformationAboutLicenseViewModel()
        {
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
