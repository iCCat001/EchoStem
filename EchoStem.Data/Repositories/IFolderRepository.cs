using EchoStem.Data;
using EchoStem.Data.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EchoStem.Data.Repositories
{
    public interface IFolderRepository
    {
        Task<List<Folder>> GetFoldersAsync();
        Task<AddFolderResult> AddFolderAsync(string path);
        Task<RemoveFolderResult> RemoveFolderAsync(long folderId);
        Task UpdateFoldersAsync(IList<Folder> folders);
    }
}
