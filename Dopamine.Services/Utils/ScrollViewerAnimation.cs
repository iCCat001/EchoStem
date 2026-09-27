using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Dopamine.Services.Utils
{
    /// <summary>
    /// Provides an animatable vertical scroll offset for a <see cref="ScrollViewer"/>. The
    /// <see cref="ScrollViewer.VerticalOffset"/> property itself cannot be animated, so the
    /// animation drives this attached property and scrolls the ScrollViewer from its callback.
    /// </summary>
    public sealed class ScrollViewerAnimation : DependencyObject
    {
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.RegisterAttached("VerticalOffset", typeof(double), typeof(ScrollViewerAnimation), new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        public static readonly DependencyProperty TargetOffsetProperty =
            DependencyProperty.RegisterAttached("TargetOffset", typeof(double), typeof(ScrollViewerAnimation), new PropertyMetadata(double.NaN));

        public static double GetVerticalOffset(DependencyObject element)
        {
            return (double)element.GetValue(VerticalOffsetProperty);
        }

        public static void SetVerticalOffset(DependencyObject element, double value)
        {
            element.SetValue(VerticalOffsetProperty, value);
        }

        public static double GetTargetOffset(DependencyObject element)
        {
            return (double)element.GetValue(TargetOffsetProperty);
        }

        public static void SetTargetOffset(DependencyObject element, double value)
        {
            element.SetValue(TargetOffsetProperty, value);
        }

        private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ScrollViewer scrollViewer = d as ScrollViewer;

            if (scrollViewer != null)
            {
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }

        /// <summary>
        /// Smoothly scrolls the given <see cref="ScrollViewer"/> to <paramref name="targetOffset"/>.
        /// As long as the target stays the same, the animation is not restarted (the lyric timer
        /// re-requests the same target every tick).
        /// </summary>
        public static void ScrollToVerticalOffset(ScrollViewer scrollViewer, double targetOffset, double durationMilliseconds)
        {
            if (scrollViewer == null)
            {
                return;
            }

            double currentTarget = GetTargetOffset(scrollViewer);

            if (!double.IsNaN(currentTarget) && Math.Abs(currentTarget - targetOffset) < 0.5)
            {
                // Already scrolling to (or already at) this offset.
                return;
            }

            SetTargetOffset(scrollViewer, targetOffset);

            double from = scrollViewer.VerticalOffset;

            if (Math.Abs(from - targetOffset) < 0.5)
            {
                // Already there: no animation required.
                SetVerticalOffset(scrollViewer, targetOffset);
                return;
            }

            DoubleAnimation animation = new DoubleAnimation
            {
                From = from,
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            scrollViewer.BeginAnimation(VerticalOffsetProperty, animation);
        }
    }
}
