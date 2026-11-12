using Microsoft.AspNetCore.Components;

namespace CoreDesign.UserJourney.Blazor;

/// <summary>
/// Records which item a page showed, for the workbook's "Items viewed" report. Renders nothing.
/// <code>&lt;JourneyView Type="Product" Item="@product.Name" /&gt;</code>
/// </summary>
public sealed class JourneyView : ComponentBase
{
    /// <summary>The kind of item, for example <c>Product</c>.</summary>
    [Parameter, EditorRequired]
    public string Type { get; set; } = string.Empty;

    /// <summary>The item shown, for example a product name or ID. Never personal data.</summary>
    [Parameter]
    public string? Item { get; set; }

    protected override void OnParametersSet() => Journey.Viewed(Type, Item);
}
