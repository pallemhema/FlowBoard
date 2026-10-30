namespace FlowBoard.Shared.DTOs.Common;

public class ErrorResponse
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? TraceId { get; set; }
}