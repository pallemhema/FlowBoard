using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FlowBoard.Api.Configuration;
using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.Authentication;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Web.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.Sqlite;

namespace FlowBoard.Tests;

public class AuthenticationSecurityTests
{
    [Fact]
    public void JwtSettings_RejectsMissingSigningKey()
    {
        var settings = CreateJwtSettings();
        settings.SecretKey = string.Empty;

        var exception = Assert.Throws<InvalidOperationException>(
            settings.Validate);

        Assert.DoesNotContain("secret", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JwtSettings_RejectsMalformedAndWeakSigningKeys()
    {
        var malformedSettings = CreateJwtSettings();
        malformedSettings.SecretKey = "not-base64";
        Assert.Throws<InvalidOperationException>(malformedSettings.Validate);

        var weakSettings = CreateJwtSettings();
        weakSettings.SecretKey = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(16));
        Assert.Throws<InvalidOperationException>(weakSettings.Validate);
    }

    [Fact]
    public void TokenService_GeneratesTokenValidatedByConfiguredSigningKey()
    {
        var settings = CreateJwtSettings();
        settings.Validate();
        var tokenService = new TokenService(Options.Create(settings));
        var userId = Guid.NewGuid();

        var token = tokenService.GenerateAccessToken(
            userId,
            "employee@example.test",
            nameof(UserRole.Employee));

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = settings.CreateSigningKey(),
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role
            },
            out _);

        Assert.Equal(userId.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(nameof(UserRole.Employee), principal.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public void RefreshTokenCookie_UsesSecureProductionOptionsAndConsistentDeletionOptions()
    {
        var environment = CreateEnvironment(Environments.Production);

        var cookie = RefreshTokenCookieOptions.Create(
            environment,
            requestIsHttps: false,
            expirationDays: 30);
        var deletionCookie = RefreshTokenCookieOptions.CreateForDeletion(
            environment,
            requestIsHttps: false);

        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal("/", cookie.Path);
        Assert.NotNull(cookie.Expires);
        Assert.True(deletionCookie.Secure);
        Assert.True(deletionCookie.HttpOnly);
        Assert.Equal(cookie.SameSite, deletionCookie.SameSite);
        Assert.Equal(cookie.Path, deletionCookie.Path);
        Assert.Null(deletionCookie.Expires);
    }

    [Fact]
    public void RefreshTokenCookie_AllowsLocalHttpButUsesSecureFlagForDevelopmentHttps()
    {
        var environment = CreateEnvironment(Environments.Development);

        Assert.False(
            RefreshTokenCookieOptions.Create(
                environment,
                requestIsHttps: false).Secure);
        Assert.True(
            RefreshTokenCookieOptions.Create(
                environment,
                requestIsHttps: true).Secure);
    }

    [Fact]
    public async Task RefreshTokenRotation_ConsumesTokenAndStoresOnlyReplacementHash()
    {
        await using var database = await TestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        await database.AddUserAndRefreshTokenAsync(userId, "original-token");
        var tokenService = new StubTokenService("replacement-token");

        await using (var context = database.CreateContext())
        {
            var result = await CreateAuthService(context, tokenService)
                .RefreshTokenAsync("original-token");

            Assert.Equal("replacement-token", result.RefreshToken);
            Assert.Equal("access-token", result.AccessToken);
        }

        await using var verificationContext = database.CreateContext();
        var originalToken = await verificationContext.RefreshTokens
            .SingleAsync(token => token.TokenHash == HashToken("original-token"));
        var replacementToken = await verificationContext.RefreshTokens
            .SingleAsync(token => token.TokenHash == HashToken("replacement-token"));

        Assert.NotNull(originalToken.RevokedAt);
        Assert.Null(replacementToken.RevokedAt);
        Assert.NotEqual("replacement-token", replacementToken.TokenHash);
    }

    [Fact]
    public async Task RefreshTokenRotation_RejectsReuseOfRevokedToken()
    {
        await using var database = await TestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        await database.AddUserAndRefreshTokenAsync(userId, "original-token");
        var tokenService = new StubTokenService("replacement-token");

        await using (var context = database.CreateContext())
        {
            await CreateAuthService(context, tokenService)
                .RefreshTokenAsync("original-token");
        }

        await using (var context = database.CreateContext())
        {
            await Assert.ThrowsAsync<UnauthorizedException>(
                () => CreateAuthService(context, tokenService)
                    .RefreshTokenAsync("original-token"));
        }

        Assert.Equal(1, tokenService.RefreshTokenGenerationCount);
        await using var verificationContext = database.CreateContext();
        Assert.Equal(2, await verificationContext.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task RefreshTokenRotation_OnlyOneConcurrentRequestConsumesToken()
    {
        await using var database = await TestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        await database.AddUserAndRefreshTokenAsync(userId, "original-token");

        var firstTokenService = new StubTokenService("replacement-one");
        var secondTokenService = new StubTokenService("replacement-two");
        var firstRequest = RefreshOnceAsync(database, firstTokenService);
        var secondRequest = RefreshOnceAsync(database, secondTokenService);
        var outcomes = await Task.WhenAll(firstRequest, secondRequest);

        Assert.Single(outcomes.Where(outcome => outcome.Succeeded));
        Assert.Single(outcomes.Where(outcome => outcome.Rejected));
        Assert.Equal(
            1,
            firstTokenService.RefreshTokenGenerationCount +
            secondTokenService.RefreshTokenGenerationCount);

        await using var verificationContext = database.CreateContext();
        Assert.Equal(2, await verificationContext.RefreshTokens.CountAsync());
        Assert.Single(await verificationContext.RefreshTokens
            .Where(token => token.RevokedAt == null)
            .ToListAsync());
    }

    [Fact]
    public async Task RefreshTokenRotation_RollsBackRevocationWhenReplacementInsertFails()
    {
        await using var database = await TestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        await database.AddUserAndRefreshTokenAsync(userId, "original-token");
        await database.AddRefreshTokenAsync(userId, "replacement-token");

        await using (var context = database.CreateContext())
        {
            await Assert.ThrowsAsync<DbUpdateException>(
                () => CreateAuthService(
                        context,
                        new StubTokenService("replacement-token"))
                    .RefreshTokenAsync("original-token"));
        }

        await using var verificationContext = database.CreateContext();
        var originalToken = await verificationContext.RefreshTokens
            .SingleAsync(token => token.TokenHash == HashToken("original-token"));
        Assert.Null(originalToken.RevokedAt);
        Assert.Equal(2, await verificationContext.RefreshTokens.CountAsync());
    }

    private static async Task<RefreshOutcome> RefreshOnceAsync(
        TestDatabase database,
        StubTokenService tokenService)
    {
        await using var context = database.CreateContext();

        try
        {
            await CreateAuthService(
                    context,
                    tokenService)
                .RefreshTokenAsync("original-token");
            return new RefreshOutcome(Succeeded: true, Rejected: false);
        }
        catch (UnauthorizedException)
        {
            return new RefreshOutcome(Succeeded: false, Rejected: true);
        }
    }

    private static AuthService CreateAuthService(
        FlowBoardDbContext context,
        ITokenService tokenService)
    {
        return new AuthService(
            context,
            new UnusedPasswordHasher(),
            tokenService,
            Options.Create(CreateJwtSettings()),
            new TestCurrentUser());
    }

    private static JwtSettings CreateJwtSettings()
    {
        return new JwtSettings
        {
            SecretKey = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32)),
            Issuer = "FlowBoard.Api",
            Audience = "FlowBoard.Web",
            AccessTokenExpirationMinutes = 5,
            RefreshTokenExpirationDays = 7
        };
    }

    private static string HashToken(string token)
    {
        return Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        return new TestHostEnvironment
        {
            EnvironmentName = environmentName,
            ApplicationName = "FlowBoard.Tests",
            ContentRootPath = AppContext.BaseDirectory,
            ContentRootFileProvider = new NullFileProvider()
        };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private sealed class StubTokenService(string replacementToken)
        : ITokenService
    {
        public int RefreshTokenGenerationCount { get; private set; }

        public string GenerateAccessToken(
            Guid userId,
            string email,
            string role)
        {
            return "access-token";
        }

        public string GenerateRefreshToken()
        {
            RefreshTokenGenerationCount++;
            return replacementToken;
        }
    }

    private sealed class UnusedPasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
            throw new NotSupportedException();
        }

        public bool Verify(string password, string passwordHash)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestCurrentUser
        : FlowBoard.Infrastructure.Data.ICurrentUserService
    {
        public Guid UserId => Guid.Empty;

        public string? Email => null;

        public string? Role => null;

        public bool IsAuthenticated => false;
    }

    private sealed record RefreshOutcome(bool Succeeded, bool Rejected);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _anchorConnection;

        private TestDatabase(
            string connectionString,
            SqliteConnection anchorConnection)
        {
            _connectionString = connectionString;
            _anchorConnection = anchorConnection;
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connectionString =
                new SqliteConnectionStringBuilder
                {
                    DataSource = $"FlowBoardTests_{Guid.NewGuid():N}",
                    Mode = SqliteOpenMode.Memory,
                    Cache = SqliteCacheMode.Shared
                }.ToString();
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync();

            var database = new TestDatabase(
                connectionString,
                anchorConnection);
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            return database;
        }

        public FlowBoardDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<FlowBoardDbContext>()
                .UseSqlite(_connectionString)
                .Options;
            return new FlowBoardDbContext(options, new UnauthenticatedCurrentUser());
        }

        private sealed class UnauthenticatedCurrentUser
            : FlowBoard.Infrastructure.Data.ICurrentUserService
        {
            public Guid UserId =>
                throw new InvalidOperationException("No authenticated user is available.");

            public string? Email => null;

            public string? Role => null;

            public bool IsAuthenticated => false;
        }

        public async Task AddUserAndRefreshTokenAsync(
            Guid userId,
            string rawToken)
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = userId,
                Email = $"{userId:N}@example.test",
                PasswordHash = "not-used",
                FirstName = "Test",
                LastName = "User",
                Role = UserRole.Employee
            };
            context.Users.Add(user);
            context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = user,
                TokenHash = HashToken(rawToken),
                ExpiresAt = now.AddDays(7)
            });
            await context.SaveChangesAsync();
        }

        public async Task AddRefreshTokenAsync(
            Guid userId,
            string rawToken)
        {
            await using var context = CreateContext();
            context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _anchorConnection.DisposeAsync();
        }
    }
}
