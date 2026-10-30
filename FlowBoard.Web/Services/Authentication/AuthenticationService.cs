using System.IdentityModel.Tokens.Jwt;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Web.Services.Api;
using FlowBoard.Web.Services.UI;
namespace FlowBoard.Web.Services.Authentication;

public class AuthenticationService :
    IAuthenticationService
{
    private readonly FlowBoardAuthenticationStateProvider
        _authenticationStateProvider;

    private readonly IAuthApiService _authApiService;

    private readonly SemaphoreSlim _refreshLock =
        new(1, 1);

    private readonly IAccessTokenProvider _accessTokenState;
    private readonly ToastService _toastService;


    public string? AccessToken =>
        _accessTokenState.GetAccessToken();


    private bool _initialized;

    public bool IsInitialized => _initialized;

    private readonly Guid _instanceId = Guid.NewGuid();

    public AuthenticationService(
        FlowBoardAuthenticationStateProvider authenticationStateProvider,
        IAuthApiService authApiService,
        IAccessTokenProvider accessTokenState,
        ToastService toastService)
    {
        _authenticationStateProvider =
            authenticationStateProvider;

        _authApiService =
            authApiService;

        _accessTokenState =
            accessTokenState;

        _toastService =
            toastService;

        Console.WriteLine(
                "========== AuthenticationService CREATED ==========");

        Console.WriteLine(
            $"AUTH SERVICE INSTANCE = {_instanceId}");
    }

    public Guid GetInstanceId()
    {
        return _instanceId;
    }

    // ============================================================
    // INITIALIZE
    // ============================================================

    public async Task<bool> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            Console.WriteLine(
                "AUTH SERVICE: Already initialized. Skipping refresh.");

            return false;
        }

        Console.WriteLine();
        Console.WriteLine(
            "========== AUTH INITIALIZATION START ==========");

        try
        {
            Console.WriteLine(
                "AUTH SERVICE: Calling RefreshAsync.");

            var response =
                await _authApiService.RefreshAsync(
                    cancellationToken);

            if (response is null)
            {
                Console.WriteLine(
                    "AUTH SERVICE: No refresh response.");

                _initialized = true;

                return false;
            }

            Console.WriteLine(
                $"AUTH SERVICE: Access token received = " +
                $"{!string.IsNullOrWhiteSpace(response.AccessToken)}");

            if (string.IsNullOrWhiteSpace(
                response.AccessToken))
            {
                Console.WriteLine(
                    "AUTH SERVICE: Access token is empty.");

                _initialized = true;

                return false;
            }

            SetAuthentication(response);

            _initialized = true;

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"AUTH SERVICE: Initialization failed: {ex}");

            _initialized = true;

            return false;
        }
        finally
        {
            Console.WriteLine(
                "========== AUTH INITIALIZATION END ==========");
        }
    }
    // ============================================================
    // LOGIN
    // ============================================================

    public async Task<bool> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AUTH SERVICE LOGIN ==========");

        var response =
            await _authApiService.LoginAsync(
                request,
                cancellationToken);

        if (response is null)
        {
            _toastService.Error(
                "Invalid email or password.");

            Console.WriteLine(
            "AUTH SERVICE: Login failed.");

            return false;
        }


        Console.WriteLine(
            "AUTH SERVICE: Login successful.");

        // IMPORTANT:
        // Login already established authentication.
        // Do NOT call RefreshAsync here.

        SetAuthentication(response);

        _initialized = true;


        _toastService.Success(
    "Login successful.");

        Console.WriteLine(
            "AUTH SERVICE: Login authentication state established.");

        return true;
    }
    // ============================================================
    // REGISTER
    // ============================================================

    public async Task<bool> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AUTH SERVICE REGISTER ==========");

        var response =
            await _authApiService.RegisterAsync(
                request,
                cancellationToken);

        if (response is null)
        {
            Console.WriteLine(
                "AUTH SERVICE: Registration failed.");

            _toastService.Error("Registration Failed");

            return false;
        }

        Console.WriteLine(
            "AUTH SERVICE: Registration successful.");

        _toastService.Success("Registration Successfull");

        SetAuthentication(response);

        _initialized = true;

        return true;
    }

    // ============================================================
    // SET AUTHENTICATION
    // ============================================================

    public void SetAuthentication(
      AuthenticationResult response)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== SET AUTHENTICATION ==========");

        Console.WriteLine(
    $"AUTH SERVICE INSTANCE = {_instanceId}");

        if (string.IsNullOrWhiteSpace(
                response.AccessToken))
        {
            Console.WriteLine(
                "AUTH SERVICE: Access token is EMPTY.");

            throw new ArgumentException(
                "Access token cannot be empty.");
        }

        // Store access token
        _accessTokenState.SetAccessToken(
            response.AccessToken);

        Console.WriteLine(
            "AUTH SERVICE: Access token stored in memory.");

        // ============================================================
        // READ JWT
        // ============================================================

        var handler =
            new JwtSecurityTokenHandler();

        var token =
            handler.ReadJwtToken(
                response.AccessToken);

        // ============================================================
        // TOKEN INFORMATION
        // ============================================================

        Console.WriteLine();
        Console.WriteLine(
            "---------- ACCESS TOKEN DETAILS ----------");

        Console.WriteLine(
            $"AUTH SERVICE: Token ID (jti) = " +
            $"{token.Id}");

        Console.WriteLine(
            $"AUTH SERVICE: Issuer = " +
            $"{token.Issuer}");

        Console.WriteLine(
            $"AUTH SERVICE: Audience = " +
            $"{string.Join(", ", token.Audiences)}");

        Console.WriteLine(
            $"AUTH SERVICE: Issued At (iat) = " +
            $"{token.IssuedAt:yyyy-MM-dd HH:mm:ss UTC}");

        Console.WriteLine(
            $"AUTH SERVICE: Expires At (exp) = " +
            $"{token.ValidTo:yyyy-MM-dd HH:mm:ss UTC}");

        Console.WriteLine(
            $"AUTH SERVICE: Current UTC = " +
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");

        var remaining =
            token.ValidTo - DateTime.UtcNow;

        Console.WriteLine(
            $"AUTH SERVICE: Token remaining lifetime = " +
            $"{remaining.TotalSeconds:F1} seconds");

        Console.WriteLine(
            "------------------------------------------");

        // ============================================================
        // CLAIMS
        // ============================================================

        var claims =
            token.Claims.ToList();

        Console.WriteLine(
            $"AUTH SERVICE: Claims received = {claims.Count}");

        foreach (var claim in claims)
        {
            Console.WriteLine(
                $"AUTH SERVICE CLAIM: {claim.Type} = {claim.Value}");
        }

        // ============================================================
        // UPDATE AUTHENTICATION STATE
        // ============================================================

        _authenticationStateProvider
            .SetAuthenticatedUser(claims);

        Console.WriteLine(
            "AUTH SERVICE: AuthenticationStateProvider UPDATED.");

        Console.WriteLine(
            "========== AUTHENTICATION SET ==========");
    }

    // ============================================================
    // REFRESH
    // ============================================================

    public async Task<bool> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {

            Console.WriteLine();
            Console.WriteLine(
                "========== AUTH SERVICE REFRESH ==========");
            Console.WriteLine(
$"AUTH SERVICE INSTANCE = {_instanceId}");

            Console.WriteLine(
                "AUTH SERVICE: Calling AuthApiService.RefreshAsync().");

            var response =
                await _authApiService.RefreshAsync(
                    cancellationToken);

            if (response is null)
            {
                Console.WriteLine(
                    "AUTH SERVICE: Refresh failed. No response.");

                return false;
            }

            Console.WriteLine(
                "AUTH SERVICE: Refresh successful.");

            SetAuthentication(response);

            Console.WriteLine(
                "AUTH SERVICE: Authentication state updated.");

            Console.WriteLine(
                "========== AUTH SERVICE REFRESH SUCCESS ==========");

            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // ============================================================
    // LOGOUT
    // ============================================================

    public async Task LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AUTH SERVICE LOGOUT ==========");

        var success =
            await _authApiService.LogoutAsync(
                cancellationToken);

        Console.WriteLine(
            $"AUTH SERVICE: Logout result = {success}");
        _accessTokenState.ClearAccessToken();
        _authenticationStateProvider.Logout();

        _initialized = true;
        _toastService.Success(
    "You have been logged out.");

        Console.WriteLine(
            "AUTH SERVICE: Local authentication state cleared.");

        Console.WriteLine(
            "========== AUTH SERVICE LOGOUT COMPLETE ==========");
    }
}