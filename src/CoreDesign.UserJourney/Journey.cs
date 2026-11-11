using System.Diagnostics;

namespace CoreDesign.UserJourney;

/// <summary>Records what a user looked at, alongside the page view or click that showed it.</summary>
public static class Journey
{
    /// <summary>
    /// Records that the current page or action showed <paramref name="item"/>, for example
    /// <c>Journey.Viewed("Product", product.Name)</c>. The workbook's "Items viewed" report groups by these
    /// values. Never pass personal data or free text the user typed.
    /// </summary>
    public static void Viewed(string itemType, string? item)
    {
        if (string.IsNullOrWhiteSpace(item) || Activity.Current is not { } activity)
            return;

        activity.SetTag(UserJourneyTags.ViewedItemType, itemType);
        activity.SetTag(UserJourneyTags.ViewedItem, item);
    }
}
