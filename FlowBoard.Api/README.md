# Authentication configuration and signing-key rotation

The API requires `Jwt:SecretKey` to be supplied by a secure configuration
provider. The value must be canonical Base64 encoding of at least 32
cryptographically random bytes. The API does not provide a signing-key default.

For local development, store a generated key in ASP.NET Core user secrets:

```sh
openssl rand -base64 32
dotnet user-secrets set "Jwt:SecretKey" "<generated-value>" --project FlowBoard.Api
```

For hosted environments, provide the secret through a managed secret store or
the `Jwt__SecretKey` environment variable. Do not put the value in source
control, deployment manifests, logs, or exception messages.

## Rotation procedure

The previously committed signing key is compromised and must not be reused.
Generate a new key in the deployment's secret-management system, replace the
configured key, and restart/redeploy the API. Signing and validation use the
same setting.

Replacing the key immediately invalidates all existing access tokens signed
with the old key. Refresh tokens are stored as hashes in the database and are
not signed with the JWT key, so existing refresh sessions remain usable and can
mint new access tokens. To terminate every existing refresh session during
rotation, also revoke all active refresh-token rows in the database:

```sql
UPDATE "RefreshTokens"
SET "RevokedAt" = CURRENT_TIMESTAMP
WHERE "RevokedAt" IS NULL;
```

Users whose refresh sessions were revoked must sign in again. If sessions are
not revoked, users can continue refreshing until their refresh tokens expire.
