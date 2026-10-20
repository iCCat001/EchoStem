using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Utils;
using Dopamine.Services.Dialog;
using Dopamine.ViewModels.Common;
using Dopamine.Views.Common;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Dopamine.Services.Update
{
    public interface IUpdateChecker
    {
        /// <summary>
        /// Checks for updates and shows the corresponding dialogs. When <paramref name="silent"/>
        /// is true, nothing is shown when the application is already up to date.
        /// </summary>
        Task CheckForUpdatesAsync(bool silent);
    }

    public class UpdateChecker : IUpdateChecker
    {
        private readonly IUpdateService updateService;
        private readonly IDialogService dialogService;

        public UpdateChecker(IUpdateService updateService, IDialogService dialogService)
        {
            this.updateService = updateService;
            this.dialogService = dialogService;
        }

        public async Task CheckForUpdatesAsync(bool silent)
        {
            try
            {
                UpdateCheckResult result = await this.updateService.CheckForUpdatesAsync();

                this.ShowResult(result, silent);
            }
            catch (Exception ex)
            {
                // A failing update check must never take the application down.
                LogClient.Error("Update check failed. Exception: {0}", ex.Message);
            }
        }

        private void ShowResult(UpdateCheckResult result, bool silent)
        {
            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    // An update is shown in both the silent and the active mode.
                    this.ShowUpdateDialog(result);
                    break;

                case UpdateCheckStatus.UpToDate:
                    if (!silent)
                    {
                        this.dialogService.ShowNotification(
                            0xe73e,
                            16,
                            ResourceUtils.GetString("Language_Check_For_Updates"),
                            ResourceUtils.GetString("Language_Current_Version_Is_Latest"),
                            ResourceUtils.GetString("Language_Close"),
                            false,
                            string.Empty);
                    }
                    break;

                default:
                    if (!silent)
                    {
                        this.dialogService.ShowNotification(
                            0xe711,
                            16,
                            ResourceUtils.GetString("Language_Check_For_Updates"),
                            ResourceUtils.GetString("Language_Update_Check_Failed"),
                            ResourceUtils.GetString("Language_Close"),
                            false,
                            string.Empty);
                    }
                    break;
            }
        }

        private void ShowUpdateDialog(UpdateCheckResult result)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var view = new UpdateDialog();
                var viewModel = new UpdateDialogViewModel(this.updateService, result);
                view.DataContext = viewModel;

                this.dialogService.ShowCustomDialog(
                    0xe895,
                    16,
                    ResourceUtils.GetString("Language_Update_Available"),
                    view,
                    520,
                    0,
                    false,
                    true,
                    true,
                    true,
                    ResourceUtils.GetString("Language_Update_Now"),
                    ResourceUtils.GetString("Language_Not_Now"),
                    viewModel.UpdateNowAsync);

                if (viewModel.UpdateStarted)
                {
                    // The update script copies the new files once this process has exited.
                    Application.Current.Shutdown();
                }
            });
        }
    }
}
