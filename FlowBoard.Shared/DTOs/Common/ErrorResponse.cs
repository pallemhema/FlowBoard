using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowBoard.Shared.DTOs.Common;

public class ErrorResponse
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? TraceId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Details { get; set; }
}