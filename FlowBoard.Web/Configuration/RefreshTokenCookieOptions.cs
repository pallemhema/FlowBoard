using Microsoft.AspNetCore.Http;

namespace FlowBoard.Web.Configuration;

public static class RefreshTokenCookieOptions
{
    public const string Name = "refreshToken";

    public static CookieOptions Create(
        IHostEnvironment environment,
        bool requestIsHttps,
        int? expirationDays = null,
        DateTimeOffset? expiresAt = null)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() || requestIsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        };

        if (expiresAt.HasValue)
        {
            options.Expires = expiresAt.Value;
        }
        else if (expirationDays.HasValue)
        {
            options.Expires = DateTimeOffset.UtcNow.AddDays(
                expirationDays.Value);
        }

        return options;
    }

    public static CookieOptions CreateForDeletion(
        IHostEnvironment environment,
        bool requestIsHttps)
    {
        return Create(environment, requestIsHttps);
    }
}