using Microsoft.IdentityModel.Tokens;

namespace FlowBoard.Api.Configuration;

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int AccessTokenExpirationMinutes { get; set; }

    public int RefreshTokenExpirationDays { get; set; }

    public void Validate()
    {
        _ = CreateSigningKey();

        if (string.IsNullOrWhiteSpace(Issuer) ||
            string.IsNullOrWhiteSpace(Audience) ||
            AccessTokenExpirationMinutes <= 0 ||
            RefreshTokenExpirationDays <= 0)
        {
            throw new InvalidOperationException(
                "JWT issuer, audience, and token expiration settings must be configured with valid values.");
        }
    }

    public SymmetricSecurityKey CreateSigningKey()
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            throw new InvalidOperationException(
                "JWT signing key configuration is missing.");
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(SecretKey);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "JWT signing key must be a Base64-encoded cryptographic key.");
        }

        if (keyBytes.Length < 32 ||
            !string.Equals(
                Convert.ToBase64String(keyBytes),
                SecretKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "JWT signing key must be canonical Base64 encoding of at least 32 bytes.");
        }

        return new SymmetricSecurityKey(keyBytes);
    }
}