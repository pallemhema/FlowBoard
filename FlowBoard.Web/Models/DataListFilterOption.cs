namespace FlowBoard.Web.Models.DataDisplay;

public sealed class DataListFilterOption<TValue>
{
    public TValue Value { get; set; } = default!;

    public string Text { get; set; } = string.Empty;
}