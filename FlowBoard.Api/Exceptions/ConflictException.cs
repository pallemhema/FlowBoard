namespace FlowBoard.Api.Exceptions;

public class ConflictException : AppException
{
    public ConflictException(string message)
        : base(
            message,
            StatusCodes.Status409Conflict,
            "CONFLICT")
    {
    }

    public ConflictException(string message, object details)
        : base(
            message,
            StatusCodes.Status409Conflict,
            "CONFLICT",
            details)
    {
    }
}