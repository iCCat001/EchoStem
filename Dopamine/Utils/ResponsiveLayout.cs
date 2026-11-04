using System;

namespace Dopamine.Utils
{
    /// <summary>
    /// Shared rules for switching the UI between its wide and its narrow ("portrait") arrangement.
    /// The values mirror the breakpoints a web page would use, so the player behaves like a fluid
    /// layout instead of a fixed desktop one:
    /// - <see cref="StackingAspectRatio"/> is the analog of the CSS
    ///   <c>@media (orientation: portrait)</c> query (= <c>max-aspect-ratio: 1/1</c>), with a small
    ///   cushion so the UI switches just before the side-by-side layout gets cramped.
    /// - <see cref="MinimumTextWidth"/> is the width the text column needs to stay readable; when the
    ///   artwork (which is sized from the available height) plus that width no longer fits, the
    ///   content has to stack even if the window is technically still landscape.
    /// </summary>
    public static class ResponsiveLayout
    {
        /// <summary>
        /// Width / height ratio below which content is stacked vertically.
        /// A web page switches at 1.0 (<c>orientation: portrait</c>); 1.15 keeps a small cushion, in
        /// the spirit of the common <c>max-aspect-ratio: 4/3</c> variant.
        /// </summary>
        public const double StackingAspectRatio = 1.15;

        /// <summary>
        /// Width (in DIPs) the text column needs before it starts truncating titles.
        /// 320 is the WCAG 1.4.10 reflow floor; the gutter is added by the caller.
        /// </summary>
        public const double MinimumTextWidth = 320.0;

        /// <summary>
        /// True when the available area is portrait-ish and the content should be stacked.
        /// </summary>
        public static bool ShouldStackVertically(double width, double height)
        {
            return width > 0 && height > 0 && width / height < StackingAspectRatio;
        }

        /// <summary>
        /// True when a side-by-side arrangement (artwork at <paramref name="artworkSize"/> plus a
        /// readable text column) no longer fits in <paramref name="width"/>.
        /// </summary>
        public static bool DoesNotFitSideBySide(double width, double artworkSize)
        {
            return width < artworkSize + MinimumTextWidth;
        }

        public static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
