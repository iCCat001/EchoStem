using Digimezzo.Foundation.Core.Utils;
using Dopamine.Services.Entities;
using Dopamine.Services.Lyrics;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dopamine.Services.Utils
{
    public sealed class ScrollUtils
    {
        public static bool IsFullyVisible(FrameworkElement child, FrameworkElement scrollViewer)
        {
            GeneralTransform childTransform = child.TransformToAncestor(scrollViewer);
            Rect childRectangle = childTransform.TransformBounds(new Rect(new Point(0, 0), child.RenderSize));
            Rect ownerRectangle = new Rect(new Point(0, 0), scrollViewer.RenderSize);

            return ownerRectangle.Contains(childRectangle.TopLeft) & ownerRectangle.Contains(childRectangle.BottomRight);
        }

        public static void ScrollToListBoxItem(ListBox box, object itemObject, bool scrollOnlyIfNotInView)
        {
            // Verify that the item is not visible. Only scroll if it is not visible.
            // ----------------------------------------------------------------------
            if (scrollOnlyIfNotInView)
            {
                ScrollViewer scrollViewer = (ScrollViewer)VisualTreeUtils.GetDescendantByType(box, typeof(ScrollViewer));
                FrameworkElement listBoxItem = (FrameworkElement)box.ItemContainerGenerator.ContainerFromItem(itemObject);

                if (scrollViewer != null && listBoxItem != null)
                {
                    if (IsFullyVisible(listBoxItem, scrollViewer))
                    {
                        return;
                    }
                }
            }

            // Scroll to the bottom of the list. This is a workaround which puts the 
            // desired Item at the top of the list when executing ScrollIntoView
            // ---------------------------------------------------------------------
            box.UpdateLayout(); // This seems required to get correct positioning.
            box.ScrollIntoView(box.Items[box.Items.Count - 1]);

            // Scroll to the desired Item
            // --------------------------
            box.UpdateLayout(); // This seems required to get correct positioning.
            box.ScrollIntoView(itemObject);
        }



        public static void ScrollToDataGridItem(DataGrid grid, object itemObject, bool scrollOnlyIfNotInView)
        {
            // Verify that the item is not visible. Only scroll if it is not visible.
            // ----------------------------------------------------------------------
            if (scrollOnlyIfNotInView)
            {
                ScrollViewer scrollViewer = (ScrollViewer)VisualTreeUtils.GetDescendantByType(grid, typeof(ScrollViewer));
                FrameworkElement listBoxItem = (FrameworkElement)grid.ItemContainerGenerator.ContainerFromItem(itemObject);

                if (scrollViewer != null && listBoxItem != null)
                {
                    if (IsFullyVisible(listBoxItem, scrollViewer))
                    {
                        return;
                    }
                }
            }

            // Scroll to the bottom of the list. This is a workaround which puts the 
            // desired Item at the top of the list when executing ScrollIntoView
            // ---------------------------------------------------------------------
            grid.UpdateLayout(); // This seems required to get correct positioning.
            grid.ScrollIntoView(grid.Items[grid.Items.Count - 1]);

            // Scroll to the desired Item
            // --------------------------
            grid.UpdateLayout(); // This seems required to get correct positioning.
            grid.ScrollIntoView(itemObject);
        }

        public static async Task ScrollToPlayingTrackAsync(ListBox box)
        {
            if (box == null) return;

            Object itemObject = null;

            await Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i <= box.Items.Count - 1; i++)
                    {
                        if (((TrackViewModel)box.Items[i]).IsPlaying)
                        {
                            itemObject = box.Items[i];
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            });

            if (itemObject == null)
            {
                return;
            }

            try
            {
                ScrollToListBoxItem(box, itemObject, true);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task ScrollToPlayingTrackAsync(DataGrid grid)
        {
            if (grid == null) return;

            Object itemObject = null;

            await Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i <= grid.Items.Count - 1; i++)
                    {
                        if (((TrackViewModel)grid.Items[i]).IsPlaying)
                        {
                            itemObject = grid.Items[i];
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            });

            if (itemObject == null) return;

            try
            {
                ScrollToDataGridItem(grid, itemObject, true);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task ScrollToHighlightedLyricsLineAsync(ListBox box)
        {
            if (box == null)
            {
                return;
            }

            object itemObject = null;

            await Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i <= box.Items.Count - 1; i++)
                    {
                        if (box.Items[i] is LyricsLineViewModel && ((LyricsLineViewModel)box.Items[i]).IsHighlighted)
                        {
                            itemObject = box.Items[i];
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            });

            if (itemObject == null) return;

            try
            {
                ScrollToListBoxItemCenteredVertically(box, itemObject);
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Scrolls the given ListBox so that the item is vertically centered in the viewport.
        public static void ScrollToListBoxItemCenteredVertically(ListBox box, object itemObject)
        {
            if (box == null || itemObject == null)
            {
                return;
            }

            ScrollViewer scrollViewer = (ScrollViewer)VisualTreeUtils.GetDescendantByType(box, typeof(ScrollViewer));

            if (scrollViewer == null)
            {
                return;
            }

            // Virtualized items which are out of view don't have a container yet: bring the item
            // into view first, so its container gets generated.
            if (box.ItemContainerGenerator.ContainerFromItem(itemObject) == null)
            {
                box.ScrollIntoView(itemObject);
                box.UpdateLayout();
            }

            FrameworkElement listBoxItem = (FrameworkElement)box.ItemContainerGenerator.ContainerFromItem(itemObject);

            if (listBoxItem == null)
            {
                return;
            }

            GeneralTransform transform = listBoxItem.TransformToAncestor(scrollViewer);
            double itemTopInViewport = transform.Transform(new Point(0, 0)).Y;
            double itemTopInContent = scrollViewer.VerticalOffset + itemTopInViewport;

            double targetOffset = itemTopInContent + (listBoxItem.ActualHeight / 2.0) - (scrollViewer.ViewportHeight / 2.0);

            double maxOffset = scrollViewer.ExtentHeight - scrollViewer.ViewportHeight;
            if (maxOffset < 0) maxOffset = 0;
            if (targetOffset < 0) targetOffset = 0;
            if (targetOffset > maxOffset) targetOffset = maxOffset;

            // Animate the scroll instead of jumping to the new offset.
            ScrollViewerAnimation.ScrollToVerticalOffset(scrollViewer, targetOffset, 350);
        }
    }
}
