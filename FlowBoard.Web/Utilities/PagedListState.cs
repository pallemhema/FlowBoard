using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Web.Utilities;

public class PagedListState<T> : AsyncOperationState
{
    public PagedResponse<T>? Data { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public int TotalCount => Data?.TotalCount ?? 0;

    public int TotalPages => Data?.TotalPages ?? 0;
}