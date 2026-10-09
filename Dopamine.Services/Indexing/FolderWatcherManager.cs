using Digimezzo.Foundation.Core.Logging;
using Dopamine.Core.Helpers;
using Dopamine.Data.Entities;
using Dopamine.Data.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Dopamine.Services.Indexing
{
    internal class FolderWatcherManager
    {
        private IFolderRepository folderRepository;
        private IList<GentleFolderWatcher> watchers = new List<GentleFolderWatcher>();

        public event EventHandler FoldersChanged = delegate { };

        public FolderWatcherManager(IFolderRepository folderRepository)
        {
            this.folderRepository = folderRepository;
        }

        private void Watcher_FolderChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.FoldersChanged(this, new EventArgs());
            });
        }

        public async Task StartWatchingAsync()
        {
            await this.StopWatchingAsync();

            List<Folder> folders = await this.folderRepository.GetFoldersAsync();

            foreach (Folder fol in folders)
            {
                if (Directory.Exists(fol.Path))
                {
                    try
                    {
                        // When the folder exists, but access is denied, creating the FileSystemWatcher throws an exception.
                        var watcher = new GentleFolderWatcher(fol.Path, true, 2000);
                        watcher.FolderChanged += Watcher_FolderChanged;

                        lock (this.watchers)
                        {
                            this.watchers.Add(watcher);
                        }

                        watcher.Resume();
                    }
                    catch (Exception ex)
                    {
                        LogClient.Error($"Could not watch folder '{fol.Path}', even though it exists. Please check folder permissions. Exception: {ex.Message}");
                    }
                }
            }
        }

        public async Task StopWatchingAsync()
        {
            // Take a snapshot and clear the shared list under a lock. The watchers are disposed on
            // a background thread. Without this, a concurrent StartWatchingAsync which adds to the
            // list while the loop below runs could make the index go out of range (and crash the
            // app through the unhandled exception handler).
            List<GentleFolderWatcher> watchersToStop;

            lock (this.watchers)
            {
                if (this.watchers.Count == 0)
                {
                    return;
                }

                watchersToStop = new List<GentleFolderWatcher>(this.watchers);
                this.watchers.Clear();
            }

            await Task.Run(() =>
            {
                foreach (GentleFolderWatcher watcher in watchersToStop)
                {
                    watcher.FolderChanged -= Watcher_FolderChanged;
                    watcher.Dispose();
                }
            });
        }
    }
}