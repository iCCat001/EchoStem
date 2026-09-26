// Copyright (C) 2011 - 2012, Jacob Johnston 
//
// Permission is hereby granted, free of charge, to any person obtaining a 
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions: 
//
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software. 
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE. 

using Dopamine.Core.Audio;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Dopamine.Controls
{
    public enum SpectrumAnimationStyle
    {
        Nervous = 1,
        Gentle
    }

    public class SpectrumAnalyzer : Control
    {
        private const double minDBValue = -90;
        private const double maxDBValue = 0;
        private const double dbScale = (maxDBValue - minDBValue);
        private const int defaultRefreshInterval = 25;

        private readonly Stopwatch renderStopwatch = new Stopwatch();
        private Canvas spectrumCanvas;
        private ISpectrumPlayer soundPlayer;
        private readonly List<Shape> barShapes = new List<Shape>();
        private double[] barHeights;
        private float[] channelData = new float[1024];
        private float[] channelPeakData;
        private double bandWidth = 1.0;
        private int maximumFrequency = 20000;
        private int maximumFrequencyIndex = 2047;
        private int minimumFrequency = 20;
        private int minimumFrequencyIndex;
        private int[] barIndexMax;
        private int[] barLogScaleIndexMax;
        private int peakFallDelay = 10;
        private bool isSubscribedToRendering;

        public static readonly DependencyProperty AnimationStyleProperty = DependencyProperty.Register("AnimationStyle", typeof(SpectrumAnimationStyle), typeof(SpectrumAnalyzer), new PropertyMetadata(SpectrumAnimationStyle.Nervous, null));

        public SpectrumAnimationStyle AnimationStyle
        {
            get
            {
                return (SpectrumAnimationStyle)GetValue(AnimationStyleProperty);
            }
            set
            {
                SetValue(AnimationStyleProperty, value);
            }
        }

        public static readonly DependencyProperty BarBackgroundProperty = DependencyProperty.Register("BarBackground", typeof(Brush), typeof(SpectrumAnalyzer), new PropertyMetadata(Brushes.White, OnBarBackgroundChanged));

        private static void OnBarBackgroundChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            SpectrumAnalyzer spectrumAnalyzer = o as SpectrumAnalyzer;

            if (spectrumAnalyzer.barShapes != null && spectrumAnalyzer.barShapes.Count > 0)
            {
                foreach (Shape bar in spectrumAnalyzer.barShapes)
                {
                    bar.Fill = spectrumAnalyzer.BarBackground;
                }
            }
        }

        public Brush BarBackground
        {
            get
            {
                return (Brush)GetValue(BarBackgroundProperty);
            }
            set
            {
                SetValue(BarBackgroundProperty, value);
            }
        }

        public static readonly DependencyProperty BarWidthProperty = DependencyProperty.Register("BarWidth", typeof(double), typeof(SpectrumAnalyzer), new UIPropertyMetadata(1.0, OnBarWidthChanged, OnCoerceBarWidth));

        private static object OnCoerceBarWidth(DependencyObject o, object value)
        {
            return value;
        }

        private static void OnBarWidthChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            SpectrumAnalyzer spectrumAnalyzer = o as SpectrumAnalyzer;

            if (spectrumAnalyzer.barShapes != null && spectrumAnalyzer.barShapes.Count > 0)
            {
                foreach (Shape bar in spectrumAnalyzer.barShapes)
                {
                    bar.Width = spectrumAnalyzer.BarWidth;
                }
            }
        }

        public double BarWidth
        {
            get
            {
                return (double)GetValue(BarWidthProperty);
            }
            set
            {
                SetValue(BarWidthProperty, value);
            }
        }

        public static readonly DependencyProperty BarCountProperty = DependencyProperty.Register("BarCount", typeof(int), typeof(SpectrumAnalyzer), new UIPropertyMetadata(32, OnBarCountChanged, OnCoerceBarCount));

        private static object OnCoerceBarCount(DependencyObject o, object value)
        {
            SpectrumAnalyzer spectrumAnalyzer = o as SpectrumAnalyzer;
            if (spectrumAnalyzer != null) return spectrumAnalyzer.OnCoerceBarCount((int)value);
            return value;
        }

        private static void OnBarCountChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            SpectrumAnalyzer spectrumAnalyzer = o as SpectrumAnalyzer;
            if (spectrumAnalyzer != null) spectrumAnalyzer.OnBarCountChanged((int)e.OldValue, (int)e.NewValue);
        }

        protected virtual int OnCoerceBarCount(int value)
        {
            value = Math.Max(value, 1);
            return value;
        }

        protected virtual void OnBarCountChanged(int oldValue, int newValue)
        {
            this.UpdateBarLayout();
        }

        public int BarCount
        {
            get
            {
                return (int)GetValue(BarCountProperty);
            }
            set
            {
                SetValue(BarCountProperty, value);
            }
        }

        public static readonly DependencyProperty BarSpacingProperty = DependencyProperty.Register("BarSpacing", typeof(double), typeof(SpectrumAnalyzer), new UIPropertyMetadata(5.0d, OnBarSpacingChanged, OnCoerceBarSpacing));

        private static object OnCoerceBarSpacing(DependencyObject o, object value)
        {
            SpectrumAnalyzer spectrumAnalyzer = o as SpectrumAnalyzer;

            if (spectrumAnalyzer != null) return spectrumAnalyzer.OnCoerceBarSpacing((double)value);
            return value;
        }

        private static void OnBarSpacingChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            SpectrumAnalyzer spectrumAnalyzer = o as SpectrumAnalyzer;
            if (spectrumAnalyzer != null) spectrumAnalyzer.OnBarSpacingChanged((double)e.OldValue, (double)e.NewValue);
        }

        protected virtual double OnCoerceBarSpacing(double value)
        {
            value = Math.Max(value, 0);
            return value;
        }

        protected virtual void OnBarSpacingChanged(double oldValue, double newValue)
        {
            this.UpdateBarLayout();
        }

        public double BarSpacing
        {
            get
            {
                return (double)GetValue(BarSpacingProperty);
            }
            set
            {
                SetValue(BarSpacingProperty, value);
            }
        }

        public static readonly DependencyProperty RefreshIntervalProperty = DependencyProperty.Register("RefreshInterval", typeof(int), typeof(SpectrumAnalyzer), new UIPropertyMetadata(defaultRefreshInterval, OnRefreshIntervalChanged, OnCoerceRefreshInterval));

        private static object OnCoerceRefreshInterval(DependencyObject o, object value)
        {
            SpectrumAnalyzer spectrumAnalyzer = o as SpectrumAnalyzer;
            if (spectrumAnalyzer != null) return spectrumAnalyzer.OnCoerceRefreshInterval((int)value);
            return value;
        }

        private static void OnRefreshIntervalChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            SpectrumAnalyzer spectrumAnalyzer = o as SpectrumAnalyzer;
            if (spectrumAnalyzer != null) spectrumAnalyzer.OnRefreshIntervalChanged((int)e.OldValue, (int)e.NewValue);
        }

        protected virtual int OnCoerceRefreshInterval(int value)
        {
            value = Math.Min(1000, Math.Max(10, value));
            return value;
        }

        protected virtual void OnRefreshIntervalChanged(int oldValue, int newValue)
        {
        }

        public int RefreshInterval
        {
            get
            {
                return (int)GetValue(RefreshIntervalProperty);
            }
            set
            {
                SetValue(RefreshIntervalProperty, value);
            }
        }

        static SpectrumAnalyzer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SpectrumAnalyzer), new FrameworkPropertyMetadata(typeof(SpectrumAnalyzer)));
        }

        public SpectrumAnalyzer()
        {
        }

        public override void OnApplyTemplate()
        {
            this.spectrumCanvas = (Canvas)GetTemplateChild("PART_SpectrumCanvas");
            this.spectrumCanvas.SizeChanged += spectrumCanvas_SizeChanged;
            this.UpdateBarLayout();
        }

        protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
        {
            base.OnTemplateChanged(oldTemplate, newTemplate);
            if (this.spectrumCanvas != null) this.spectrumCanvas.SizeChanged -= spectrumCanvas_SizeChanged;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            this.UpdateBarLayout();
            this.UpdateSpectrum();
        }

        public void RegisterSoundPlayer(ISpectrumPlayer soundPlayer)
        {
            this.UnregisterSoundPlayer();

            this.soundPlayer = soundPlayer;
            this.UpdateBarLayout();
            this.StartRendering();
        }

        public void UnregisterSoundPlayer()
        {
            this.StopRendering();

            if (soundPlayer != null)
            {
                this.soundPlayer.Unregister();
                this.soundPlayer = null;
            }
        }

        private void StartRendering()
        {
            if (this.isSubscribedToRendering)
            {
                return;
            }

            this.isSubscribedToRendering = true;
            this.renderStopwatch.Restart();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void StopRendering()
        {
            if (!this.isSubscribedToRendering)
            {
                return;
            }

            this.isSubscribedToRendering = false;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (this.renderStopwatch.ElapsedMilliseconds < this.RefreshInterval)
            {
                return;
            }

            this.renderStopwatch.Restart();
            this.UpdateSpectrum();
        }

        private void UpdateSpectrum()
        {
            if (this.soundPlayer == null || this.spectrumCanvas == null || this.spectrumCanvas.RenderSize.Width < 1 || this.spectrumCanvas.RenderSize.Height < 1)
            {
                return;
            }

            if (this.soundPlayer.IsPlaying && !this.soundPlayer.GetFFTData(ref this.channelData))
            {
                return;
            }

            this.UpdateSpectrumShapes();
        }

        private void UpdateSpectrumShapes()
        {
            try
            {
                bool allZero = true;
                double fftBucketHeight = 0f;
                double barHeight = 0f;
                double lastPeakHeight = 0f;
                double peakYPos = 0f;
                double height = spectrumCanvas.RenderSize.Height;
                int barIndex = 0;

                for (int i = this.minimumFrequencyIndex; i <= this.maximumFrequencyIndex; i++)
                {
                    // If we're paused, keep drawing, but set the current height to 0 so the peaks fall.
                    if (!this.soundPlayer.IsPlaying)
                    {
                        barHeight = 0f;
                    }
                    else // Draw the maximum value for the bar's band
                    {
                        switch (this.AnimationStyle)
                        {
                            case SpectrumAnimationStyle.Nervous:
                                // Do nothing
                                break;
                            case SpectrumAnimationStyle.Gentle:
                                this.channelData[i] -= 0.003f;
                                break;
                            default:
                                break;
                        }

                        double dbValue = 20 * Math.Log10((double)this.channelData[i]);

                        fftBucketHeight = ((dbValue - minDBValue) / dbScale) * height;

                        if (barHeight < fftBucketHeight)
                        {
                            barHeight = fftBucketHeight;
                        }

                        if (barHeight < 0f)
                        {
                            barHeight = 0f;
                        }
                    }

                    // If this is the last FFT bucket in the bar's group, draw the bar.
                    if (i == this.barIndexMax[barIndex])
                    {
                        // Peaks can't surpass the height of the control.
                        if (barHeight > height)
                        {
                            barHeight = height;
                        }

                        peakYPos = barHeight;

                        if (this.channelPeakData[barIndex] < peakYPos)
                        {
                            this.channelPeakData[barIndex] = (float)peakYPos;
                        }
                        else
                        {
                            this.channelPeakData[barIndex] = (float)(peakYPos + (this.peakFallDelay * this.channelPeakData[barIndex])) / ((float)(this.peakFallDelay + 1));
                        }

                        double scaleY = height > 0 ? Math.Min(barHeight / height, 1.0) : 0;
                        double peakScaleY = height > 0 ? Math.Min(this.channelPeakData[barIndex] / height, 1.0) : 0;

                        switch (this.AnimationStyle)
                        {
                            case SpectrumAnimationStyle.Nervous:
                                ((ScaleTransform)this.barShapes[barIndex].RenderTransform).ScaleY = scaleY;
                                break;
                            case SpectrumAnimationStyle.Gentle:
                                ((ScaleTransform)this.barShapes[barIndex].RenderTransform).ScaleY = peakScaleY;
                                break;
                            default:
                                break;
                        }

                        if (this.channelPeakData[barIndex] > 0.05)
                        {
                            allZero = false;
                        }

                        lastPeakHeight = barHeight;
                        barHeight = 0f;
                        barIndex++;
                    }
                }

                if (!allZero || this.soundPlayer.IsPlaying)
                {
                    return;
                }

                // When paused and all bars have fallen to zero, there is nothing left to draw.
            }
            catch (IndexOutOfRangeException)
            {
                // Intended suppression.
            }
        }

        private void UpdateBarLayout()
        {
            if (this.soundPlayer == null || this.spectrumCanvas == null) return;

            this.maximumFrequencyIndex = Math.Min(this.soundPlayer.GetFFTFrequencyIndex(this.maximumFrequency) + 1, 2047);
            this.minimumFrequencyIndex = Math.Min(this.soundPlayer.GetFFTFrequencyIndex(this.minimumFrequency), 2047);
            this.bandWidth = Math.Max(((double)(this.maximumFrequencyIndex - this.minimumFrequencyIndex)) / this.spectrumCanvas.RenderSize.Width, 1.0);

            int actualBarCount;

            if (this.BarWidth >= 1.0d)
            {
                actualBarCount = this.BarCount;
            }
            else
            {
                actualBarCount = Math.Max((int)((this.spectrumCanvas.RenderSize.Width - this.BarSpacing) / (this.BarWidth + this.BarSpacing)), 1);
            }

            this.channelPeakData = new float[actualBarCount];

            int indexCount = this.maximumFrequencyIndex - this.minimumFrequencyIndex;
            int linearIndexBucketSize = (int)Math.Round((double)indexCount / (double)actualBarCount, 0);
            var maxIndexList = new List<int>();
            var maxLogScaleIndexList = new List<int>();
            double maxLog = Math.Log(actualBarCount, actualBarCount);

            for (int i = 1; i < actualBarCount; i++)
            {
                maxIndexList.Add(this.minimumFrequencyIndex + (i * linearIndexBucketSize));
                int logIndex = (int)((maxLog - Math.Log((actualBarCount + 1) - i, (actualBarCount + 1))) * indexCount) + this.minimumFrequencyIndex;
                maxLogScaleIndexList.Add(logIndex);
            }

            maxIndexList.Add(this.maximumFrequencyIndex);
            maxLogScaleIndexList.Add(this.maximumFrequencyIndex);
            this.barIndexMax = maxIndexList.ToArray();
            this.barLogScaleIndexMax = maxLogScaleIndexList.ToArray();

            this.barHeights = new double[actualBarCount];

            this.spectrumCanvas.Children.Clear();
            this.barShapes.Clear();

            double height = this.spectrumCanvas.RenderSize.Height;

            for (int i = 0; i < actualBarCount; i++)
            {
                double xCoord = this.BarSpacing + (this.BarWidth * i) + (this.BarSpacing * i) + 1;
                var barRectangle = new Rectangle()
                {
                    Width = this.BarWidth,
                    Height = height > 0 ? height : 1,
                    Fill = this.BarBackground,
                    RenderTransformOrigin = new Point(0.5, 1),
                    RenderTransform = new ScaleTransform(1, 0)
                };

                Canvas.SetLeft(barRectangle, xCoord);
                Canvas.SetTop(barRectangle, 0);

                this.barShapes.Add(barRectangle);
            }

            foreach (Shape shape in barShapes) this.spectrumCanvas.Children.Add(shape);
        }

        private void spectrumCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateBarLayout();
        }
    }
}