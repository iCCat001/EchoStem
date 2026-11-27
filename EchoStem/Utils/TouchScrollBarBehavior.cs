using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace EchoStem.Utils
{
    /// <summary>
    /// Touch scroll bar interaction.
    ///
    /// In touch optimization mode the first touch on a vertical scroll bar does not jump to the touched
    /// position. Instead the bar expands to the left (twice its width) and shows a floating bubble with
    /// the first visible character of the content. That "standby" state stays until the user touches
    /// somewhere outside the scroll bar. While in standby:
    ///   - touching another position in the bar scrolls to that position,
    ///   - dragging the thumb (or the bubble) scrolls and the bubble follows.
    ///
    /// Mouse input is not changed: the behavior only reacts to touch events.
    /// </summary>
    public static class TouchScrollBarBehavior
    {
        private const double BubbleDiameter = 46.0;
        private const double BubbleGap = 8.0;
        private const double ExpansionFactor = 2.0;

        private static readonly ConditionalWeakTable<ScrollBar, ScrollBarState> States =
            new ConditionalWeakTable<ScrollBar, ScrollBarState>();

        private static ScrollBarState activeState;

        /// <summary>Attached to every ScrollBar by the implicit ScrollBar style (see ScrollBar.xaml).</summary>
        public static readonly DependencyProperty IsAttachedProperty =
            DependencyProperty.RegisterAttached("IsAttached", typeof(bool), typeof(TouchScrollBarBehavior),
                new PropertyMetadata(false, OnIsAttachedChanged));

        public static void SetIsAttached(DependencyObject element, bool value)
        {
            element.SetValue(IsAttachedProperty, value);
        }

        public static bool GetIsAttached(DependencyObject element)
        {
            return (bool)element.GetValue(IsAttachedProperty);
        }

        /// <summary>Collapses the standby state, e.g. when touch optimization is switched off.</summary>
        public static void DeactivateAll()
        {
            ScrollBarState state = activeState;

            if (state != null)
            {
                Collapse(state);
            }
        }

        private static void OnIsAttachedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var bar = d as ScrollBar;

            if (bar == null)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                Attach(bar);
            }
            else
            {
                Detach(bar);
            }
        }

        private static void Attach(ScrollBar bar)
        {
            if (States.TryGetValue(bar, out _))
            {
                return;
            }

            var state = new ScrollBarState(bar);
            States.Add(bar, state);

            bar.Loaded += (sender, e) => OnBarLoaded(state);
            bar.Unloaded += (sender, e) => Collapse(state);
            bar.PreviewTouchDown += (sender, e) => OnBarTouchDown(state, e);
            bar.PreviewTouchMove += (sender, e) => OnBarTouchMove(state, e);
            bar.PreviewTouchUp += (sender, e) => OnBarTouchUp(state, e);
            bar.PreviewMouseLeftButtonDown += (sender, e) => OnBarMouseDown(state, e);
            bar.ValueChanged += (sender, e) => NotifyScrolled(state);
        }

        private static void Detach(ScrollBar bar)
        {
            if (!States.TryGetValue(bar, out ScrollBarState state))
            {
                return;
            }

            Collapse(state);
            States.Remove(bar);
        }

        private static void OnBarLoaded(ScrollBarState state)
        {
            ScrollBar bar = state.Bar;

            bar.ApplyTemplate();

            if (state.Track == null)
            {
                state.Track = bar.Template == null ? null : bar.Template.FindName("PART_Track", bar) as Track;

                if (state.Track != null)
                {
                    state.Thumb = state.Track.Thumb;
                }
            }

            if (state.Viewer == null)
            {
                state.Viewer = bar.TemplatedParent as ScrollViewer;

                if (state.Viewer == null)
                {
                    state.Viewer = FindAncestor<ScrollViewer>(bar);
                }

                if (state.Viewer != null)
                {
                    state.ContentPresenter =
                        state.Viewer.Template == null
                            ? null
                            : state.Viewer.Template.FindName("PART_ScrollContentPresenter", state.Viewer) as ScrollContentPresenter;

                    if (state.ContentPresenter == null)
                    {
                        state.ContentPresenter = FindDescendant<ScrollContentPresenter>(state.Viewer);
                    }

                    state.Viewer.ScrollChanged += (sender, e) => NotifyScrolled(state);
                }
            }

            bar.SizeChanged += (sender, e) =>
            {
                if (state.Standby)
                {
                    NotifyScrolled(state);
                }
            };
        }

        private static void OnBarTouchDown(ScrollBarState state, TouchEventArgs e)
        {
            if (!TouchOptimization.IsEnabled || state.Bar.Orientation != Orientation.Vertical)
            {
                return;
            }

            if (state.Track == null || state.Thumb == null || state.Viewer == null)
            {
                return;
            }

            // A stray contact (second finger, palm, pen) must not interrupt an ongoing bubble drag.
            if (state.DraggingBubble)
            {
                return;
            }

            if (!state.Standby)
            {
                // First touch: only expand and show the bubble, do not scroll (that is the whole point).
                e.Handled = EnterStandby(state);
                return;
            }

            double positionY = e.GetTouchPoint(state.Bar).Position.Y;

            if (IsInside(e.OriginalSource as DependencyObject, state.Thumb) || IsNearThumb(state, positionY))
            {
                // Drag the thumb ourselves. Left to the native Thumb it would be handled as a touch
                // panning gesture by the ScrollViewer (which scrolls in the opposite direction).
                // Around the thumb a generous touch band is accepted, so the thumb is easy to grab.
                e.Handled = true;
                BeginBubbleDrag(state, positionY, e.TouchDevice, false);
                state.Bar.CaptureTouch(e.TouchDevice);
                return;
            }

            e.Handled = true;
            JumpToPosition(state, positionY);
        }

        /// <summary>
        /// True when the touch position is close enough to the thumb to start dragging it. The thumb
        /// itself is only a few pixels thick, which is hard to hit with a finger; the band around it
        /// makes grabbing it reliable in touch mode.
        /// </summary>
        private static bool IsNearThumb(ScrollBarState state, double positionY)
        {
            if (state.Thumb == null || state.Viewer == null || state.Bar.Maximum <= state.Bar.Minimum)
            {
                return false;
            }

            Rect range = GetThumbRange(state);
            double value = state.Viewer.VerticalOffset;
            double fraction = Clamp((value - state.Bar.Minimum) / (state.Bar.Maximum - state.Bar.Minimum), 0, 1);
            double centerY = range.Top + fraction * range.Height;
            double halfBand = Math.Max(state.Thumb.ActualHeight / 2.0, 24.0);

            return positionY >= centerY - halfBand && positionY <= centerY + halfBand;
        }

        private static void OnBarTouchMove(ScrollBarState state, TouchEventArgs e)
        {
            if (!state.DraggingBubble || state.DragIsMouse || !IsDragTouchDevice(state, e.TouchDevice))
            {
                return;
            }

            e.Handled = true;
            ContinueBubbleDrag(state, e.GetTouchPoint(state.Bar).Position.Y);
        }

        private static void OnBarTouchUp(ScrollBarState state, TouchEventArgs e)
        {
            if (!state.DraggingBubble || state.DragIsMouse || !IsDragTouchDevice(state, e.TouchDevice))
            {
                return;
            }

            e.Handled = true;
            EndBubbleDrag(state);
        }

        private static void OnBarMouseDown(ScrollBarState state, MouseButtonEventArgs e)
        {
            if (!TouchOptimization.IsEnabled || state.Bar.Orientation != Orientation.Vertical)
            {
                return;
            }

            if (state.Track == null || state.Thumb == null || state.Viewer == null)
            {
                return;
            }

            if (state.DraggingBubble)
            {
                return;
            }

            bool onThumb = IsInside(e.OriginalSource as DependencyObject, state.Thumb);

            if (!state.Standby)
            {
                // A mouse button does the same as a finger: the first press on the scroll bar enters
                // standby. On the thumb the native drag is kept (the bubble simply appears with it);
                // on the track the press is consumed so the scroll does not jump immediately.
                bool entered = EnterStandby(state);

                if (entered && !onThumb)
                {
                    e.Handled = true;
                }

                return;
            }

            if (onThumb)
            {
                return;
            }

            e.Handled = true;
            JumpToPosition(state, e.GetPosition(state.Bar).Y);
        }

        private static bool EnterStandby(ScrollBarState state)
        {
            ScrollBar bar = state.Bar;

            AdornerLayer layer = AdornerLayer.GetAdornerLayer(bar);

            if (layer == null)
            {
                // Without an adorner layer (e.g. some popup contents) the bubble cannot be shown,
                // so the scroll bar keeps its normal behavior instead of becoming unusable.
                return false;
            }

            if (bar.ActualWidth <= 0)
            {
                return false;
            }

            state.DefaultWidth = bar.ActualWidth;
            state.DefaultThumbThickness = GetThumbThickness(state);
            state.Standby = true;
            activeState = state;

            // Expand to the left: the element becomes twice as wide, but the space it takes in the
            // ScrollViewer grid stays the same (negative left margin), so it overlays the content.
            bar.Width = state.DefaultWidth * ExpansionFactor;
            bar.HorizontalAlignment = HorizontalAlignment.Right;
            bar.Margin = new Thickness(-state.DefaultWidth, 0, 0, 0);

            // Override the thumb thickness for this scroll bar only: the drawn slider becomes twice
            // as thick while the scroll bar is in standby, so the expanded state is visible.
            bar.Resources["Touch_ScrollBarThumbThickness"] = state.DefaultThumbThickness * ExpansionFactor;

            CreateBubble(state);
            layer.Add(state.Adorner);

            // While the scroll bar is in standby the list must not react to its own touch panning:
            // otherwise pressing the (touch sized) thumb turns into a panning gesture and the list
            // scrolls in the opposite direction.
            SuspendPanning(state);

            HookWindow(state);
            NotifyScrolled(state);
            return true;
        }

        private static void Collapse(ScrollBarState state)
        {
            if (!state.Standby)
            {
                return;
            }

            state.Standby = false;

            if (ReferenceEquals(activeState, state))
            {
                activeState = null;
            }

            UnhookWindow(state);

            state.DraggingBubble = false;
            state.DragIsMouse = false;
            state.DragTouchDevice = null;
            RestorePanning(state);

            if (state.Bubble != null)
            {
                if (Mouse.Captured == state.Bubble)
                {
                    state.Bubble.ReleaseMouseCapture();
                }
            }

            if (state.Adorner != null)
            {
                AdornerLayer layer = AdornerLayer.GetAdornerLayer(state.Bar);

                if (layer != null)
                {
                    layer.Remove(state.Adorner);
                }

                state.Adorner = null;
                state.Bubble = null;
                state.Letter = null;
            }

            state.Bar.Resources.Remove("Touch_ScrollBarThumbThickness");
            state.Bar.ClearValue(FrameworkElement.WidthProperty);
            state.Bar.ClearValue(FrameworkElement.MarginProperty);
            state.Bar.ClearValue(FrameworkElement.HorizontalAlignmentProperty);
        }

        private static void CreateBubble(ScrollBarState state)
        {
            var bubble = new Border
            {
                Width = BubbleDiameter,
                Height = BubbleDiameter,
                CornerRadius = new CornerRadius(BubbleDiameter / 2.0),
                IsHitTestVisible = true,
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Direction = 270, Opacity = 0.28 }
            };

            bubble.SetResourceReference(Border.BackgroundProperty, "Brush_Accent");

            var letter = new TextBlock
            {
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            bubble.Child = letter;

            // The bubble is moved with a render transform (no layout/arrange invalidation): this is
            // cheap and does not disturb the touch capture while the finger is dragging.
            bubble.RenderTransform = new TranslateTransform();

            bubble.PreviewTouchDown += (sender, e) => OnBubbleTouchDown(state, e);
            bubble.PreviewTouchMove += (sender, e) => OnBubbleTouchMove(state, e);
            bubble.PreviewTouchUp += (sender, e) => OnBubbleTouchUp(state, e);
            bubble.LostTouchCapture += (sender, e) => OnBubbleLostTouchCapture(state, e);

            bubble.PreviewMouseLeftButtonDown += (sender, e) => OnBubbleMouseDown(state, e);
            bubble.PreviewMouseMove += (sender, e) => OnBubbleMouseMove(state, e);
            bubble.PreviewMouseLeftButtonUp += (sender, e) => OnBubbleMouseUp(state, e);

            state.Bubble = bubble;
            state.Letter = letter;
            state.Adorner = new BubbleAdorner(state.Bar, bubble);
        }

        private static void OnBubbleTouchDown(ScrollBarState state, TouchEventArgs e)
        {
            if (state.Bubble == null || !state.Standby || state.DraggingBubble)
            {
                return;
            }

            e.Handled = true;
            BeginBubbleDrag(state, e.GetTouchPoint(state.Bar).Position.Y, e.TouchDevice, false);
            state.Bubble.CaptureTouch(e.TouchDevice);
        }

        private static void OnBubbleTouchMove(ScrollBarState state, TouchEventArgs e)
        {
            if (!state.DraggingBubble || state.Bubble == null || state.DragIsMouse || !IsDragTouchDevice(state, e.TouchDevice))
            {
                return;
            }

            e.Handled = true;
            ContinueBubbleDrag(state, e.GetTouchPoint(state.Bar).Position.Y);
        }

        private static void OnBubbleTouchUp(ScrollBarState state, TouchEventArgs e)
        {
            if (!state.DraggingBubble || state.Bubble == null || state.DragIsMouse || !IsDragTouchDevice(state, e.TouchDevice))
            {
                return;
            }

            e.Handled = true;
            EndBubbleDrag(state);
        }

        private static void OnBubbleLostTouchCapture(ScrollBarState state, TouchEventArgs e)
        {
            // Only stop the drag when the finger that started it is gone. Other contacts (a second
            // finger, a palm, the pen) must not be able to reset the drag to its starting point.
            if (state.DraggingBubble && IsDragTouchDevice(state, e.TouchDevice))
            {
                EndBubbleDrag(state);
            }
        }

        private static bool IsDragTouchDevice(ScrollBarState state, TouchDevice device)
        {
            return state.DragTouchDevice != null && ReferenceEquals(state.DragTouchDevice, device);
        }

        private static void OnBubbleMouseDown(ScrollBarState state, MouseButtonEventArgs e)
        {
            if (state.Bubble == null || !state.Standby || state.DraggingBubble)
            {
                return;
            }

            e.Handled = true;
            BeginBubbleDrag(state, e.GetPosition(state.Bar).Y, null, true);
            state.Bubble.CaptureMouse();
        }

        private static void OnBubbleMouseMove(ScrollBarState state, MouseEventArgs e)
        {
            if (!state.DraggingBubble || state.Bubble == null || !state.DragIsMouse)
            {
                return;
            }

            e.Handled = true;
            ContinueBubbleDrag(state, e.GetPosition(state.Bar).Y);
        }

        private static void OnBubbleMouseUp(ScrollBarState state, MouseButtonEventArgs e)
        {
            if (!state.DraggingBubble || state.Bubble == null || !state.DragIsMouse)
            {
                return;
            }

            e.Handled = true;
            EndBubbleDrag(state);
        }

        private static void BeginBubbleDrag(ScrollBarState state, double positionY, TouchDevice touchDevice, bool isMouse)
        {
            state.DraggingBubble = true;
            state.DragIsMouse = isMouse;
            state.DragTouchDevice = touchDevice;
            state.DragStartY = positionY;
            state.DragStartValue = state.Viewer == null
                ? state.Bar.Value
                : (state.Bar.Orientation == Orientation.Vertical ? state.Viewer.VerticalOffset : state.Viewer.HorizontalOffset);
        }

        private static void ContinueBubbleDrag(ScrollBarState state, double positionY)
        {
            ScrollBar bar = state.Bar;
            Rect range = GetThumbRange(state);

            if (range.Height <= 0)
            {
                return;
            }

            double deltaValue = (positionY - state.DragStartY) / range.Height * (bar.Maximum - bar.Minimum);
            double value = Clamp(state.DragStartValue + deltaValue, bar.Minimum, bar.Maximum);

            ScrollToValue(state, value);

            // While dragging, the drag handler is the only writer of the bubble position. ScrollChanged/
            // ValueChanged events can still carry the previous offset for a moment; if they updated the
            // bubble too, it would visibly jump back and forth (seen on some touch screens).
            UpdateBubblePosition(state, value);
            ScheduleLetterUpdate(state);
        }

        private static void EndBubbleDrag(ScrollBarState state)
        {
            state.DraggingBubble = false;
            state.DragIsMouse = false;
            state.DragTouchDevice = null;

            if (state.Bubble != null)
            {
                state.Bubble.ReleaseMouseCapture();
            }
        }

        private static void SuspendPanning(ScrollBarState state)
        {
            if (state.Viewer == null || state.SavedPanningMode.HasValue)
            {
                return;
            }

            state.SavedPanningMode = state.Viewer.PanningMode;
            state.Viewer.PanningMode = PanningMode.None;
        }

        private static void RestorePanning(ScrollBarState state)
        {
            if (state.Viewer == null || !state.SavedPanningMode.HasValue)
            {
                return;
            }

            state.Viewer.PanningMode = state.SavedPanningMode.Value;
            state.SavedPanningMode = null;
        }

        private static void JumpToPosition(ScrollBarState state, double positionY)
        {
            ScrollBar bar = state.Bar;
            Rect range = GetThumbRange(state);

            double value;

            if (range.Height <= 0)
            {
                value = bar.Minimum;
            }
            else
            {
                double fraction = Clamp((positionY - range.Top) / range.Height, 0, 1);
                value = bar.Minimum + fraction * (bar.Maximum - bar.Minimum);
            }

            ScrollToValue(state, value);
            NotifyScrolled(state, value);
        }

        /// <summary>
        /// Scrolls the owning ScrollViewer. ScrollBar.Value is only template bound to the offset in our
        /// templates, so setting Value would not scroll; the ScrollViewer has to be moved directly.
        /// </summary>
        private static void ScrollToValue(ScrollBarState state, double value)
        {
            if (state.Viewer == null)
            {
                return;
            }

            if (state.Bar.Orientation == Orientation.Vertical)
            {
                state.Viewer.ScrollToVerticalOffset(value);
            }
            else
            {
                state.Viewer.ScrollToHorizontalOffset(value);
            }
        }

        /// <summary>
        /// The vertical range in which the center of the thumb can move (in ScrollBar coordinates).
        /// Our template has no arrow buttons, so the track spans the whole bar.
        /// </summary>
        private static double GetThumbThickness(ScrollBarState state)
        {
            object value = state.Bar.TryFindResource("Touch_ScrollBarThumbThickness");

            if (value is double)
            {
                return (double)value;
            }

            return 5.0;
        }

        private static Rect GetThumbRange(ScrollBarState state)
        {
            double thumbHeight = state.Thumb == null ? 0 : state.Thumb.ActualHeight;
            double barHeight = state.Bar.ActualHeight;
            double height = Math.Max(0, barHeight - thumbHeight);

            return new Rect(0, thumbHeight / 2.0, 0, height);
        }

        private static void NotifyScrolled(ScrollBarState state, double? valueOverride = null)
        {
            if (!state.Standby)
            {
                return;
            }

            if (state.DraggingBubble)
            {
                // The bubble drag handler owns the position while dragging (see ContinueBubbleDrag).
                ScheduleLetterUpdate(state);
                return;
            }

            // The position is calculated from the scroll value: it does not depend on layout, so the
            // bubble keeps up with the thumb while it is being dragged.
            UpdateBubblePosition(state, valueOverride);
            ScheduleLetterUpdate(state);
        }

        private static void UpdateBubblePosition(ScrollBarState state, double? valueOverride = null)
        {
            if (!state.Standby || state.Adorner == null || state.Thumb == null)
            {
                return;
            }

            double value = valueOverride ?? (state.Viewer == null
                ? state.Bar.Value
                : (state.Bar.Orientation == Orientation.Vertical ? state.Viewer.VerticalOffset : state.Viewer.HorizontalOffset));

            Rect range = GetThumbRange(state);

            double fraction = state.Bar.Maximum > state.Bar.Minimum
                ? Clamp((value - state.Bar.Minimum) / (state.Bar.Maximum - state.Bar.Minimum), 0, 1)
                : 0;

            double centerY = range.Top + fraction * range.Height;
            double y = Clamp(centerY - BubbleDiameter / 2.0, 0, Math.Max(0, state.Bar.ActualHeight - BubbleDiameter));

            var translate = state.Bubble.RenderTransform as TranslateTransform;

            if (translate != null)
            {
                translate.X = -(BubbleDiameter + BubbleGap);
                translate.Y = y;
            }
        }

        private static void ScheduleLetterUpdate(ScrollBarState state)
        {
            if (!state.Standby || state.UpdatePending || state.Letter == null)
            {
                return;
            }

            state.UpdatePending = true;

            state.Bar.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    state.UpdatePending = false;

                    if (!state.Standby || state.Letter == null)
                    {
                        return;
                    }

                    state.Letter.Text = GetFirstVisibleCharacter(state);
                    state.Letter.Foreground = GetContrastingForeground(state);
                }),
                DispatcherPriority.Render);
        }

        private static string GetFirstVisibleCharacter(ScrollBarState state)
        {
            ScrollViewer viewer = state.Viewer;

            if (viewer == null)
            {
                return string.Empty;
            }

            // Only the content is searched: DataGrid column headers live outside the
            // ScrollContentPresenter, so they can no longer win the "first character" (e.g. "歌").
            FrameworkElement contentArea = state.ContentPresenter;
            DependencyObject root = contentArea != null ? (DependencyObject)contentArea : viewer;

            // When CanContentScroll is true (all virtualized item lists), ScrollViewer.ViewportWidth/
            // Height are expressed in items, not in pixels. The content presenter gives the real pixel
            // viewport, which is what the text bounds must be compared against.
            double viewportWidth = contentArea != null ? contentArea.ActualWidth : viewer.ViewportWidth;
            double viewportHeight = contentArea != null ? contentArea.ActualHeight : viewer.ViewportHeight;

            if (viewportWidth <= 1 || viewportHeight <= 1)
            {
                return string.Empty;
            }

            var viewport = new Rect(0, 0, viewportWidth, viewportHeight);
            Visual coordinateRoot = contentArea != null ? (Visual)contentArea : viewer;
            TextBlock best = null;
            Rect bestBounds = Rect.Empty;

            WalkTextBlocks(root, textBlock =>
            {
                if (!textBlock.IsVisible || string.IsNullOrWhiteSpace(textBlock.Text))
                {
                    return;
                }

                if (textBlock.ActualWidth <= 0 || textBlock.ActualHeight <= 0)
                {
                    return;
                }

                Rect bounds;

                try
                {
                    bounds = textBlock.TransformToAncestor(coordinateRoot)
                        .TransformBounds(new Rect(0, 0, textBlock.ActualWidth, textBlock.ActualHeight));
                }
                catch (Exception)
                {
                    return;
                }

                if (!bounds.IntersectsWith(viewport))
                {
                    return;
                }

                if (best == null ||
                    bounds.Top < bestBounds.Top - 0.5 ||
                    (Math.Abs(bounds.Top - bestBounds.Top) <= 0.5 && bounds.Left < bestBounds.Left))
                {
                    best = textBlock;
                    bestBounds = bounds;
                }
            });

            if (best == null)
            {
                return string.Empty;
            }

            foreach (char character in best.Text)
            {
                if (!char.IsWhiteSpace(character))
                {
                    return char.ToUpperInvariant(character).ToString();
                }
            }

            return string.Empty;
        }

        private static void WalkTextBlocks(DependencyObject root, Action<TextBlock> visit)
        {
            if (root == null)
            {
                return;
            }

            int count;

            try
            {
                count = VisualTreeHelper.GetChildrenCount(root);
            }
            catch (Exception)
            {
                return;
            }

            for (int index = 0; index < count; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);

                var textBlock = child as TextBlock;
                if (textBlock != null)
                {
                    visit(textBlock);
                }

                WalkTextBlocks(child, visit);
            }
        }

        private static Brush GetContrastingForeground(ScrollBarState state)
        {
            var accent = state.Bar.TryFindResource("Brush_Accent") as SolidColorBrush;
            Color color = accent == null ? Colors.Black : accent.Color;
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;

            return luminance > 0.65 ? Brushes.Black : Brushes.White;
        }

        private static void HookWindow(ScrollBarState state)
        {
            state.Window = Window.GetWindow(state.Bar);

            if (state.Window == null)
            {
                return;
            }

            state.Window.PreviewTouchDown += OnWindowPreviewTouchDown;
            state.Window.PreviewMouseDown += OnWindowPreviewMouseDown;
        }

        private static void UnhookWindow(ScrollBarState state)
        {
            if (state.Window == null)
            {
                return;
            }

            state.Window.PreviewTouchDown -= OnWindowPreviewTouchDown;
            state.Window.PreviewMouseDown -= OnWindowPreviewMouseDown;
            state.Window = null;
        }

        private static void OnWindowPreviewTouchDown(object sender, TouchEventArgs e)
        {
            HandleWindowPress(e.OriginalSource as DependencyObject);
        }

        private static void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HandleWindowPress(e.OriginalSource as DependencyObject);
        }

        private static void HandleWindowPress(DependencyObject source)
        {
            ScrollBarState state = activeState;

            if (state == null || source == null)
            {
                return;
            }

            if (IsInside(source, state.Bar) || IsInside(source, state.Bubble))
            {
                return;
            }

            // Do not collapse while the bubble is being dragged: a second contact (palm, another
            // finger) outside the bar would otherwise collapse the standby state mid drag.
            if (state.DraggingBubble)
            {
                return;
            }

            Collapse(state);
        }

        private static bool IsInside(DependencyObject source, DependencyObject container)
        {
            if (source == null || container == null)
            {
                return false;
            }

            DependencyObject current = source;

            while (current != null)
            {
                if (ReferenceEquals(current, container))
                {
                    return true;
                }

                current = GetParent(current);
            }

            return false;
        }

        private static DependencyObject GetParent(DependencyObject element)
        {
            if (element == null)
            {
                return null;
            }

            if (element is Visual || element is System.Windows.Media.Media3D.Visual3D)
            {
                DependencyObject parent = VisualTreeHelper.GetParent(element);

                if (parent != null)
                {
                    return parent;
                }
            }

            return LogicalTreeHelper.GetParent(element);
        }

        private static T FindDescendant<T>(DependencyObject element) where T : DependencyObject
        {
            if (element == null)
            {
                return null;
            }

            int count;

            try
            {
                count = VisualTreeHelper.GetChildrenCount(element);
            }
            catch (Exception)
            {
                return null;
            }

            for (int index = 0; index < count; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, index);
                var typed = child as T;

                if (typed != null)
                {
                    return typed;
                }

                T result = FindDescendant<T>(child);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static T FindAncestor<T>(DependencyObject element) where T : DependencyObject
        {
            DependencyObject current = element;

            while (current != null)
            {
                var typed = current as T;

                if (typed != null)
                {
                    return typed;
                }

                current = GetParent(current);
            }

            return null;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return value < minimum ? minimum : (value > maximum ? maximum : value);
        }

        private sealed class ScrollBarState
        {
            public ScrollBarState(ScrollBar bar)
            {
                this.Bar = bar;
            }

            public ScrollBar Bar { get; private set; }
            public ScrollViewer Viewer { get; set; }
            public ScrollContentPresenter ContentPresenter { get; set; }
            public Track Track { get; set; }
            public Thumb Thumb { get; set; }
            public BubbleAdorner Adorner { get; set; }
            public Border Bubble { get; set; }
            public TextBlock Letter { get; set; }
            public Window Window { get; set; }
            public double DefaultWidth { get; set; }
            public double DefaultThumbThickness { get; set; }
            public bool Standby { get; set; }
            public bool UpdatePending { get; set; }
            public bool DraggingBubble { get; set; }
            public bool DragIsMouse { get; set; }
            public TouchDevice DragTouchDevice { get; set; }
            public PanningMode? SavedPanningMode { get; set; }
            public double DragStartY { get; set; }
            public double DragStartValue { get; set; }
        }

        /// <summary>Draws the character bubble left of the scroll bar (outside of its bounds).</summary>
        private sealed class BubbleAdorner : Adorner
        {
            private readonly Border bubble;

            public BubbleAdorner(UIElement adornedElement, Border bubble)
                : base(adornedElement)
            {
                this.bubble = bubble;
                this.IsHitTestVisible = true;
                this.AddVisualChild(bubble);
                this.AddLogicalChild(bubble);
            }

            protected override int VisualChildrenCount
            {
                get { return 1; }
            }

            protected override Visual GetVisualChild(int index)
            {
                return this.bubble;
            }

            protected override Size MeasureOverride(Size constraint)
            {
                this.bubble.Measure(new Size(BubbleDiameter, BubbleDiameter));
                return new Size(BubbleDiameter, BubbleDiameter);
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                this.bubble.Arrange(new Rect(0, 0, BubbleDiameter, BubbleDiameter));
                return finalSize;
            }
        }
    }
}
