using EchoStem.Data.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EchoStem.Data.Repositories
{
    public interface IQueuedTrackRepository
    {
        Task<List<QueuedTrack>> GetSavedQueuedTracksAsync();
        Task SaveQueuedTracksAsync(IList<QueuedTrack> tracks);
        Task<QueuedTrack> GetPlayingTrackAsync();
    }
}
