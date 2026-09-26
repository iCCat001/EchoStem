using System.Windows;
using System.Windows.Controls;

namespace Dopamine.Views.Common
{
    public partial class PlaybackControlsWithPlaylistNotification : UserControl
    {
        /// <summary>
        /// Optional replacement for the default <see cref="Dopamine.Views.PlaybackControls"/>.
        /// The mini players use it to host their whole control bar (playlist button, playback
        /// controls and volume) so the lyrics/notification overlay covers all of it.
        /// </summary>
        public static readonly DependencyProperty ControlsContentProperty =
            DependencyProperty.Register(nameof(ControlsContent), typeof(object), typeof(PlaybackControlsWithPlaylistNotification), new PropertyMetadata(null));

        public object ControlsContent
        {
            get { return GetValue(ControlsContentProperty); }
            set { SetValue(ControlsContentProperty, value); }
        }

        /// <summary>
        /// When true, the lyric line is left aligned (used by the mini players to fit their
        /// narrower design). The full player keeps it centered.
        /// </summary>
        public static readonly DependencyProperty LeftAlignLyricsProperty =
            DependencyProperty.Register(nameof(LeftAlignLyrics), typeof(bool), typeof(PlaybackControlsWithPlaylistNotification), new PropertyMetadata(false));

        public bool LeftAlignLyrics
        {
            get { return (bool)GetValue(LeftAlignLyricsProperty); }
            set { SetValue(LeftAlignLyricsProperty, value); }
        }

        /// <summary>
        /// Left offset applied to the left aligned lyric, used by the mini players to line the
        /// lyric up with the title/artist shown above the control bar.
        /// </summary>
        public static readonly DependencyProperty LyricsLeftMarginProperty =
            DependencyProperty.Register(nameof(LyricsLeftMargin), typeof(double), typeof(PlaybackControlsWithPlaylistNotification), new PropertyMetadata(0d));

        public double LyricsLeftMargin
        {
            get { return (double)GetValue(LyricsLeftMarginProperty); }
            set { SetValue(LyricsLeftMarginProperty, value); }
        }

        /// <summary>
        /// Right offset applied to the left aligned lyric, used by the mini players to keep the
        /// lyric from running into the cover art when it is too long.
        /// </summary>
        public static readonly DependencyProperty LyricsRightMarginProperty =
            DependencyProperty.Register(nameof(LyricsRightMargin), typeof(double), typeof(PlaybackControlsWithPlaylistNotification), new PropertyMetadata(0d));

        public double LyricsRightMargin
        {
            get { return (double)GetValue(LyricsRightMarginProperty); }
            set { SetValue(LyricsRightMarginProperty, value); }
        }

        public PlaybackControlsWithPlaylistNotification()
        {
            InitializeComponent();
        }
    }
}
