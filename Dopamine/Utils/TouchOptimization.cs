using Digimezzo.Foundation.Core.Settings;
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dopamine.Utils
{
    /// <summary>
    /// Applies the "touch optimization" feature: a set of application-wide resource values which
    /// the styles use (through DynamicResource), so the UI can adapt to touch input. When the
    /// feature is turned off, the resource values fall back to the original (mouse oriented) ones.
    /// </summary>
    public static class TouchOptimization
    {
        // Remembers the original style of each progress slider thumb, so it can be restored when
        // touch optimization is turned off. Weak keys, so unused sliders don't leak.
        private static readonly ConditionalWeakTable<Button, object> originalThumbStyles = new ConditionalWeakTable<Button, object>();

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    return SettingsClient.Get<bool>("Features", "TouchOptimization");
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>Applies the current setting to the application resources.</summary>
        public static void Apply()
        {
            if (Application.Current == null)
            {
                return;
            }

            bool on = IsEnabled;
            ResourceDictionary resources = Application.Current.Resources;

            // Taller list rows.
            resources["Touch_ListRowHeight"] = on ? 44.0 : 32.0;

            // Wider and always visible scroll bars. The visible bar itself keeps a constant, thin
            // thickness (Touch_ScrollBarThumbThickness); only the touch target becomes wider.
            resources["Touch_ScrollBarWidth"] = on ? 16.0 : 5.0;
            resources["Touch_ScrollBarThumbThickness"] = 5.0;
            resources["Touch_ScrollBarOpacity"] = on ? 1.0 : 0.0;

            // Tooltips are hover-triggered: they are useless on a touch screen.
            resources["Touch_ToolTipVisibility"] = on ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// Makes the thumb (the draggable circle) of a progress slider always visible when touch
        /// optimization is enabled. Normally the slider's template fades it in/out while the mouse
        /// hovers over it, which is useless on a touch screen.
        /// </summary>
        public static void UpdateProgressThumb(Control slider)
        {
            if (slider == null)
            {
                return;
            }

            slider.ApplyTemplate();

            if (slider.Template == null)
            {
                return;
            }

            var button = slider.Template.FindName("PART_Button", slider) as Button;

            if (button == null)
            {
                return;
            }

            if (!IsEnabled)
            {
                RestoreThumbStyle(button);
                return;
            }

            MakeThumbAlwaysVisible(button);
        }

        // Replaces the thumb's style with an identical one which has no hover triggers, so the thumb
        // stays visible instead of fading out when the mouse leaves the slider. Because the style's
        // triggers are the only source of the fade, this works no matter how the mouse is moved.
        private static void MakeThumbAlwaysVisible(Button button)
        {
            button.BeginAnimation(UIElement.OpacityProperty, null);
            button.Opacity = 1.0;

            if (originalThumbStyles.TryGetValue(button, out _))
            {
                return;
            }

            Style original = button.Style;

            if (original == null || original.Triggers.Count == 0)
            {
                return;
            }

            originalThumbStyles.Add(button, original);

            // The thumb's circle is drawn by its template: keep the colors it currently has.
            button.ApplyTemplate();
            var oldBorder = button.Template == null ? null : button.Template.FindName("PART_Border", button) as Border;
            Brush background = oldBorder == null ? null : oldBorder.Background;
            Brush borderBrush = oldBorder == null ? null : oldBorder.BorderBrush;

            var replacement = new Style(original.TargetType);

            foreach (SetterBase setterBase in original.Setters)
            {
                var setter = setterBase as Setter;

                if (setter != null)
                {
                    replacement.Setters.Add(new Setter(setter.Property, setter.Value));
                }
            }

            replacement.Setters.Add(new Setter(UIElement.OpacityProperty, 1.0));

            button.Style = replacement;
            button.BeginAnimation(UIElement.OpacityProperty, null);
            button.ApplyTemplate();

            var newBorder = button.Template == null ? null : button.Template.FindName("PART_Border", button) as Border;

            if (newBorder != null)
            {
                newBorder.Background = background;
                newBorder.BorderBrush = borderBrush;
            }
        }

        private static void RestoreThumbStyle(Button button)
        {
            if (originalThumbStyles.TryGetValue(button, out object original))
            {
                originalThumbStyles.Remove(button);
                button.Style = (Style)original;
            }

            button.BeginAnimation(UIElement.OpacityProperty, null);
            button.Opacity = 0.0;
        }
    }
}
