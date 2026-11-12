namespace FlowBoard.Api.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }

    public string ErrorCode { get; }

    public object? Details { get; }

    public AppException(
        string message,
        int statusCode,
        string errorCode,
        object? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Details = details;
    }
}