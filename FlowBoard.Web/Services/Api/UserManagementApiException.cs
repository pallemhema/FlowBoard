using System.Net;

namespace FlowBoard.Web.Services.Api;

public sealed class UserManagementApiException : Exception
{
    public UserManagementApiException(
        string message,
        HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
