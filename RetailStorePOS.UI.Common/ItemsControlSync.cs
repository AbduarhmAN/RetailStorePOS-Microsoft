using System.Collections;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Controls;

namespace RetailStorePOS.UI.Common;

/// <summary>
/// Mirrors a .NET collection into an <see cref="ItemCollection"/> from code-behind.
/// Under Native AOT the CsWinRT projection of .NET generic collections to WinRT
/// <c>IVector</c> crashes in <c>IItemsControlMethods.set_ItemsSource</c> when the
/// x:Bind compiled binding assigns <c>ItemsSource</c>, aborting the whole binding pass
/// and blanking the page. Populating <c>control.Items</c> directly sidesteps the
/// projection entirely. Shared extraction of the pattern used in
/// CheckoutPage / LoginPage / TaxConfigurationPage.
/// </summary>
public static class ItemsControlSync
{
    /// <summary>
    /// Applies a single <see cref="NotifyCollectionChangedEventArgs"/> to <paramref name="target"/>.
    /// <paramref name="source"/> is only used for the <see cref="NotifyCollectionChangedAction.Reset"/> rebuild.
    /// </summary>
    public static void Apply(IList source, ItemCollection target, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    int index = e.NewStartingIndex;
                    foreach (var item in e.NewItems)
                    {
                        if (index >= 0 && index <= target.Count)
                            target.Insert(index++, item);
                        else
                            target.Add(item);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    int index = e.OldStartingIndex;
                    if (index >= 0)
                    {
                        for (int i = 0; i < e.OldItems.Count; i++)
                            target.RemoveAt(index);
                    }
                    else
                    {
                        foreach (var item in e.OldItems)
                            target.Remove(item);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.NewItems != null && e.OldItems != null && e.NewStartingIndex >= 0)
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                        target[e.NewStartingIndex + i] = e.NewItems[i];
                }
                break;
            case NotifyCollectionChangedAction.Move:
                if (e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0)
                {
                    var item = target[e.OldStartingIndex];
                    target.RemoveAt(e.OldStartingIndex);
                    target.Insert(e.NewStartingIndex, item);
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                target.Clear();
                foreach (var item in source)
                    target.Add(item);
                break;
        }
    }

    /// <summary>Clears <paramref name="target"/> and rebuilds it from <paramref name="source"/>.</summary>
    public static void Reset(IList source, ItemCollection target)
        => Apply(source, target, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
}
