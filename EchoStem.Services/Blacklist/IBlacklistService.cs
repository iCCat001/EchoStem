using EchoStem.Services.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EchoStem.Services.Blacklist
{
    public interface IBlacklistService
    {
        Task AddToBlacklistAsync(IList<TrackViewModel> selectedTracks);

        Task RemoveFromBlacklistAsync(long blacklistTrackId);

        Task RemoveAllFromBlacklistAsync();

        Task<bool> IsInBlacklistAsync(TrackViewModel track);

        event Action<int> AddedTracksToBacklist;

        Task<IList<BlacklistTrackViewModel>> GetBlacklistTracksAsync();
    }
}
