namespace FlowBoard.Web.Services.Authentication;

public interface IAccessTokenProvider
{
    string? GetAccessToken();

    void SetAccessToken(string accessToken);

    void ClearAccessToken();

    Guid GetInstanceId();
}