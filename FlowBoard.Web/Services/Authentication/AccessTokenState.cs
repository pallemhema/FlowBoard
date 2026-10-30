namespace FlowBoard.Web.Services.Authentication;

public class AccessTokenState : IAccessTokenProvider
{
    private readonly Guid _instanceId = Guid.NewGuid();

    private string? _accessToken { get; set;}

    public AccessTokenState()
    {
        Console.WriteLine(
            "========== ACCESS TOKEN STATE CREATED ==========");

        Console.WriteLine(
            $"ACCESS TOKEN STATE INSTANCE = {_instanceId}");
    }


    public string? GetAccessToken()
    {
        return _accessToken;
    }

    public void SetAccessToken(string accessToken)
    {
        _accessToken = accessToken;

        Console.WriteLine(
            $"ACCESS TOKEN STATE {_instanceId}: TOKEN SET");
    }

    public void ClearAccessToken()
    {
        _accessToken = null;

        Console.WriteLine(
            $"ACCESS TOKEN STATE {_instanceId}: TOKEN CLEARED");
    }

    public Guid GetInstanceId()
    {
        return _instanceId;
    }
}