using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Utils;
using EchoStem.Utils;
using EchoStem.Core.Prism;
using EchoStem.Services.Playback;
using CommonServiceLocator;
using Prism.Events;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using EchoStem.Services.Utils;

namespace EchoStem.Views.Common
{
    public partial class LyricsControl : UserControl
    {
        // When the user scrolls the lyrics list manually, the automatic "scroll to the highlighted
        // line" is paused so the user can look at other lyrics. It resumes after this delay without
        // any scroll activity.
        private const int AutoScrollResumeDelaySeconds = 8;

        // Our own animated scroll also raises ScrollChanged; this grace period makes sure those
        // changes are not mistaken for user scrolling (the animation runs for 350 ms).
        private const int ProgrammaticScrollGraceMilliseconds = 400;

        private IPlaybackService playbackService;
        private IEventAggregator eventAggregator;
        private ListBox lyricsListBox;
        private TextBox lyricsTextBox;
        private ScrollViewer lyricsScrollViewer;
        private DispatcherTimer autoScrollResumeTimer;
        private DispatcherTimer programmaticScrollTimer;
        private bool autoScrollPaused;
        private bool programmaticScrollInProgress;

        public LyricsControl()
        {
            InitializeComponent();

            this.playbackService = ServiceLocator.Current.GetInstance<IPlaybackService>();
            this.eventAggregator = ServiceLocator.Current.GetInstance<IEventAggregator>();
            this.eventAggregator.GetEvent<ScrollToHighlightedLyricsLine>().Subscribe((_) => this.ScrollToHighlightedLyricsLineAsync());

            this.Loaded += this.LyricsControl_Loaded;
            this.Unloaded += this.LyricsControl_Unloaded;
            this.IsVisibleChanged += this.LyricsControl_IsVisibleChanged;
        }

        private void LyricsControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.ResumeAutoScrollAndCenter();
        }

        private void LyricsControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The lyrics page is navigated away and back again: forget a pause that belonged to the
            // previous visit and center the current line again.
            if (e.NewValue is bool && (bool)e.NewValue)
            {
                this.ResumeAutoScrollAndCenter();
            }
        }

        /// <summary>
        /// Clears a stale "user is scrolling" pause (e.g. after leaving and re-entering the page) and
        /// centers the current line once the layout is ready.
        /// </summary>
        private void ResumeAutoScrollAndCenter()
        {
            this.ResetAutoScrollState();

            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => this.ScrollToHighlightedLyricsLineAsync()));
        }

        private void ResetAutoScrollState()
        {
            this.autoScrollPaused = false;

            if (this.autoScrollResumeTimer != null)
            {
                this.autoScrollResumeTimer.Stop();
            }

            if (this.programmaticScrollTimer != null)
            {
                this.programmaticScrollTimer.Stop();
            }

            this.programmaticScrollInProgress = false;
        }
    
        private void LyricsListBox_Loaded(object sender, RoutedEventArgs e)
        {
            // This is a workaround to be able to access the LyricsListBox which is in the DataTemplate.
            try
            {
                this.lyricsListBox = sender as ListBox;
                this.AttachLyricsScrollViewer();
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not get lyricsListBox from the DataTemplate. Exception: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Watches the lyrics scroll viewer: scroll changes that we did not trigger ourselves mean the
        /// user is browsing the lyrics, so the automatic centering is paused for a while.
        /// </summary>
        private void AttachLyricsScrollViewer()
        {
            if (this.lyricsListBox == null || this.lyricsScrollViewer != null)
            {
                return;
            }

            this.lyricsScrollViewer = VisualTreeUtils.GetDescendantByType(this.lyricsListBox, typeof(ScrollViewer)) as ScrollViewer;

            if (this.lyricsScrollViewer != null)
            {
                this.lyricsScrollViewer.ScrollChanged += this.LyricsScrollViewer_ScrollChanged;
            }

            // Pause immediately on an actual scroll input, without waiting for ScrollChanged. This
            // also covers scroll bar drags and gestures while an automatic scroll is animating.
            if (this.lyricsListBox != null)
            {
                this.lyricsListBox.PreviewMouseWheel += this.LyricsListBox_PreviewMouseWheel;
                this.lyricsListBox.PreviewMouseLeftButtonDown += this.LyricsListBox_PreviewMouseLeftButtonDown;
                this.lyricsListBox.PreviewTouchDown += this.LyricsListBox_PreviewTouchDown;
                this.lyricsListBox.PreviewKeyDown += this.LyricsListBox_PreviewKeyDown;
            }
        }

        private void LyricsListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            this.PauseAutoScroll();
        }

        private void LyricsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.PauseAutoScroll();
        }

        private void LyricsListBox_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            this.PauseAutoScroll();
        }

        private void LyricsListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.PageUp || e.Key == Key.PageDown || e.Key == Key.Home || e.Key == Key.End ||
                e.Key == Key.Up || e.Key == Key.Down)
            {
                this.PauseAutoScroll();
            }
        }

        private void LyricsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange == 0 || this.programmaticScrollInProgress)
            {
                return;
            }

            this.PauseAutoScroll();
        }

        private void PauseAutoScroll()
        {
            this.autoScrollPaused = true;

            if (this.autoScrollResumeTimer == null)
            {
                this.autoScrollResumeTimer = new DispatcherTimer(DispatcherPriority.Background);
                this.autoScrollResumeTimer.Tick += this.AutoScrollResumeTimer_Tick;
            }

            this.autoScrollResumeTimer.Stop();
            this.autoScrollResumeTimer.Interval = TimeSpan.FromSeconds(AutoScrollResumeDelaySeconds);
            this.autoScrollResumeTimer.Start();
        }

        private void AutoScrollResumeTimer_Tick(object sender, EventArgs e)
        {
            if (this.autoScrollResumeTimer != null)
            {
                this.autoScrollResumeTimer.Stop();
            }

            this.autoScrollPaused = false;

            // Put the current lyric back in the center after the user stopped browsing.
            this.ScrollToHighlightedLyricsLineAsync();
        }

        private void MarkProgrammaticScroll()
        {
            this.programmaticScrollInProgress = true;

            if (this.programmaticScrollTimer == null)
            {
                this.programmaticScrollTimer = new DispatcherTimer(DispatcherPriority.Background);
                this.programmaticScrollTimer.Tick += (sender, e) =>
                {
                    if (this.programmaticScrollTimer != null)
                    {
                        this.programmaticScrollTimer.Stop();
                    }

                    this.programmaticScrollInProgress = false;
                };
            }

            this.programmaticScrollTimer.Stop();
            this.programmaticScrollTimer.Interval = TimeSpan.FromMilliseconds(ProgrammaticScrollGraceMilliseconds);
            this.programmaticScrollTimer.Start();
        }

        private void LyricsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.lyricsScrollViewer != null)
            {
                this.lyricsScrollViewer.ScrollChanged -= this.LyricsScrollViewer_ScrollChanged;
                this.lyricsScrollViewer = null;
            }

            if (this.lyricsListBox != null)
            {
                this.lyricsListBox.PreviewMouseWheel -= this.LyricsListBox_PreviewMouseWheel;
                this.lyricsListBox.PreviewMouseLeftButtonDown -= this.LyricsListBox_PreviewMouseLeftButtonDown;
                this.lyricsListBox.PreviewTouchDown -= this.LyricsListBox_PreviewTouchDown;
                this.lyricsListBox.PreviewKeyDown -= this.LyricsListBox_PreviewKeyDown;
            }

            // Do not keep a pause (and its stale timer) across leaving the page: the next visit has to
            // center the current lyric again. Otherwise the list would stay where it was scrolled to.
            this.ResetAutoScrollState();
        }

        private void LyricsTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            // This is a workaround to be able to access the LyricsTextBox which is in the DataTemplate.
            try
            {
                this.lyricsTextBox = sender as TextBox;
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not get lyricsTextBox from the DataTemplate. Exception: {0}", ex.Message);
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.T & Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (!this.playbackService.IsStopped) this.AddTimeStampToSelectedLyricsLine();
            }
        }

        private async void ScrollToHighlightedLyricsLineAsync()
        {
            if (this.lyricsListBox == null) return;

            // The user is currently browsing the lyrics: do not pull the list back to the
            // highlighted line. The resume timer does it once the user stopped scrolling.
            if (this.autoScrollPaused) return;

            try
            {
                // When shutting down, Application.Current is null
                if (Application.Current != null)
                {
                    this.MarkProgrammaticScroll();

                    await Application.Current.Dispatcher.Invoke(async () =>
                    {
                        await ScrollUtils.ScrollToHighlightedLyricsLineAsync(this.lyricsListBox);
                    });
                }
            }
            catch (Exception ex)
            {
                LogClient.Error("Could not scroll to the highlighted lyrics line. Exception: {0}", ex.Message);
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (this.lyricsTextBox != null)
            {
                try
                {
                    // Using the Dispatcher seems to be the only way to ever make the TextBox focus.
                    // See: http://stackoverflow.com/questions/13955340/keyboard-focus-does-not-work-on-text-box-in-wpf
                    Dispatcher.BeginInvoke(DispatcherPriority.Input,
                          new Action(delegate ()
                          {
                              this.lyricsTextBox.Focus(); // Set Logical Focus
                              Keyboard.Focus(this.lyricsTextBox); // Set Keyboard Focus (this is probably not needed)
                          }));
                }
                catch (Exception ex)
                {
                    LogClient.Error("Could not set focus on lyricsTextBox. Exception: {0}", ex.Message);
                }

            }
        }

        private void AddTimeStampToSelectedLyricsLine()
        {
            if (this.lyricsTextBox != null)
            {
                try
                {
                    int lineIndex = this.lyricsTextBox.GetLineIndexFromCharacterIndex(this.lyricsTextBox.CaretIndex);
                    int lineStartIndex = this.lyricsTextBox.GetCharacterIndexFromLineIndex(lineIndex);
                    string line = this.lyricsTextBox.GetLineText(lineIndex);

                    if (line.Trim().Length == 0)
                    {
                        this.lyricsTextBox.CaretIndex += 1; // Jump to the next line
                        return; // Don't try to add a timeStamp to an empty line (Trim removes newline characters)
                    }

                    string strippedLine = string.Empty;

                    if (line.Length > 0 && line.StartsWith("["))
                    {
                        int index = line.IndexOf(']');

                        if (index > 0)
                        {
                            strippedLine = line.Substring(index + 1);
                        }
                    }
                    else
                    {
                        strippedLine = line;
                    }

                    TimeSpan currentPlaybackTime = this.playbackService.GetCurrentTime;

                    int minutes = currentPlaybackTime.Minutes + currentPlaybackTime.Hours * 60;
                    int seconds = currentPlaybackTime.Seconds;
                    int milliseconds = currentPlaybackTime.Milliseconds;

                    string format = string.Format("[{0:00}:{1:00}.{2:000}]", minutes, seconds, milliseconds);
                    string newLine = string.Format("{0}{1}", new DateTime(currentPlaybackTime.Ticks).ToString(format), strippedLine);

                    this.lyricsTextBox.Text = this.lyricsTextBox.Text.Remove(lineStartIndex, line.Length);
                    this.lyricsTextBox.Text = this.lyricsTextBox.Text.Insert(lineStartIndex, newLine);
                    this.lyricsTextBox.CaretIndex = lineStartIndex + newLine.Length;

                    // Jump over empty lines
                    line = this.lyricsTextBox.GetLineText(this.lyricsTextBox.GetLineIndexFromCharacterIndex(this.lyricsTextBox.CaretIndex));

                    while (line.Trim().Length == 0)
                    {
                        // Make sure to get out of the loop if an empty line was found at the end of the lyrics
                        if (this.lyricsTextBox.CaretIndex == this.lyricsTextBox.Text.Length) break;
                        this.lyricsTextBox.CaretIndex += 1;
                        line = this.lyricsTextBox.GetLineText(this.lyricsTextBox.GetLineIndexFromCharacterIndex(this.lyricsTextBox.CaretIndex));
                    }
                }
                catch (Exception ex)
                {
                    LogClient.Error("Could not add timeStamp to selected lyrics line. Exception: {0}", ex.Message);
                }
            }
        }
    }
}