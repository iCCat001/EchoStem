namespace EchoStem.Core.Enums
{
    public enum OobePage
    {
        // First page of the wizard, only shown when data from the old Dopamine installation was found.
        Migration = 0,
        Welcome = 1,
        Language = 2,
        Appearance = 3,
        Collection = 4,
        Donate = 5,
        Finish = 6
    }

    public enum NowPlayingPage
    {
        NothingPlaying = 0,
        NowPlaying = 1
    }

    public enum NowPlayingSubPage
    {
        ShowCase = 0,
        Playlist = 1,
        Lyrics = 2,
        ArtistInformation = 3
    }

    public enum CollectionPage
    {
        Albums = 0,
        Songs = 1,
        Artists = 2,
        Playlists = 3
    }

    public enum SettingsPage
    {
        Appearance = 0,
        Behaviour = 1,
        Playback = 2,
        Startup = 3,
        Online = 4,
        Blacklist = 5,
        Features = 6,
        Plugins = 7
    }

    public enum InformationPage
    {
        Help = 0,
        About = 1
    }

    public enum FullPlayerPage
    {
        Collection = 0,
        Settings = 1,
        Information = 2
    }

    public enum EditTrackPage
    {
        Tags = 0,
        Lyrics = 1
    }
}
