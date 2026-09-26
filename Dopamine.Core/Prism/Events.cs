using Digimezzo.Foundation.WPF.Controls;
using Dopamine.Core.Enums;
using Prism.Events;
using System;

namespace Dopamine.Core.Prism
{
    public class ScrollToPlayingTrack : PubSubEvent<object>
    {
    }

    public class PerformSemanticJump : PubSubEvent<Tuple<string, string>>
    {
    }

    public class ShellMouseUp : PubSubEvent<string>
    {
    }

    public class ScrollToHighlightedLyricsLine : PubSubEvent<object>
    {
    }

    public class ToggledCoverPlayerAlignPlaylistVertically : PubSubEvent<bool>
    {
    }

    public class IsNowPlayingPageActiveChanged : PubSubEvent<bool>
    {
    }

    public class IsNowPlayingSubPageChanged : PubSubEvent<Tuple<SlideDirection, NowPlayingSubPage>>
    {
    }

    public class IsCollectionPageChanged : PubSubEvent<Tuple<SlideDirection, CollectionPage>>
    {
    }

    public class IsSettingsPageChanged : PubSubEvent<Tuple<SlideDirection, SettingsPage>>
    {
    }

    public class IsInformationPageChanged : PubSubEvent<Tuple<SlideDirection, InformationPage>>
    {
    }

    public class FocusSearchBox : PubSubEvent<object>
    {
    }

    public class ActiveSubfolderChanged : PubSubEvent<object>
    {
    }

    public class ToggleArtistOrderCommand : PubSubEvent<object>
    {
    }

    /// <summary>
    /// Requests pages which are currently not shown to release their retained lists, to save
    /// memory (e.g. when switching to the mini player or minimizing to the tray). They are
    /// reloaded when shown again.
    /// </summary>
    public class ReleaseInactivePageLists : PubSubEvent<object>
    {
    }
}