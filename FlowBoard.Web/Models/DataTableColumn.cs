using Microsoft.AspNetCore.Components;

namespace FlowBoard.Web.Models.DataDisplay;

public sealed class DataTableColumn<TItem>
{
    public string Header { get; set; } = string.Empty;

    public RenderFragment<TItem> Template { get; set; } = default!;

    public string? HeaderClass { get; set; }

    public string? CellClass { get; set; }
}