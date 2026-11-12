namespace FlowBoard.Web.Utilities;

public class AsyncOperationState
{
    public bool IsLoading { get; set; }

    public bool IsSubmitting { get; set; }

    public string? Error { get; set; }
}