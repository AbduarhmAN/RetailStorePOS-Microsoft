using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RetailStorePOS.App;

public static class NestedScrollViewerBehavior
{
    public static readonly DependencyProperty BubbleMouseWheelProperty = DependencyProperty.RegisterAttached(
        "BubbleMouseWheel",
        typeof(bool),
        typeof(NestedScrollViewerBehavior),
        new PropertyMetadata(false, OnBubbleMouseWheelChanged));

    public static bool GetBubbleMouseWheel(DependencyObject obj)
    {
        return (bool)obj.GetValue(BubbleMouseWheelProperty);
    }

    public static void SetBubbleMouseWheel(DependencyObject obj, bool value)
    {
        obj.SetValue(BubbleMouseWheelProperty, value);
    }

    private static void OnBubbleMouseWheelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not UIElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.PreviewMouseWheel += Element_PreviewMouseWheel;
        }
        else
        {
            element.PreviewMouseWheel -= Element_PreviewMouseWheel;
        }
    }

    private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject source || e.Handled)
        {
            return;
        }

        var innerScrollViewer = source as ScrollViewer ?? FindDescendant<ScrollViewer>(source);
        if (innerScrollViewer == null)
        {
            return;
        }

        if (CanScroll(innerScrollViewer, e.Delta))
        {
            return;
        }

        var parentScrollViewer = FindAncestor<ScrollViewer>(source);
        if (parentScrollViewer == null)
        {
            return;
        }

        e.Handled = true;

        var mouseWheelEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };

        parentScrollViewer.RaiseEvent(mouseWheelEvent);
    }

    private static bool CanScroll(ScrollViewer scrollViewer, int delta)
    {
        if (scrollViewer.ScrollableHeight <= 0)
        {
            return false;
        }

        return delta > 0
            ? scrollViewer.VerticalOffset > 0
            : scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var current = GetParent(child);

        while (current != null)
        {
            if (current is T target)
            {
                return target;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T target)
            {
                return target;
            }

            var nestedTarget = FindDescendant<T>(child);
            if (nestedTarget != null)
            {
                return nestedTarget;
            }
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject child)
    {
        return child switch
        {
            Visual or Visual3D => VisualTreeHelper.GetParent(child),
            FrameworkContentElement frameworkContentElement => frameworkContentElement.Parent,
            _ => null
        };
    }
}
