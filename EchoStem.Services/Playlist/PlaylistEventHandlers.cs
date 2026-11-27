using EchoStem.Services.Entities;

namespace EchoStem.Services.Playlist
{
    public delegate void TracksAddedHandler(int numberTracksAdded, string playlistName);
    public delegate void TracksDeletedHandler(PlaylistViewModel playlist);
}
