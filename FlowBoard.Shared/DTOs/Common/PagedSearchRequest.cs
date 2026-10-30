namespace FlowBoard.Shared.DTOs.Common;

public class PagedSearchRequest : PagedRequest
{
    public string? Search { get; set; }
}