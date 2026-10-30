using System.Security.Cryptography;
using System.Text;

using FlowBoard.Api.Configuration;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.CurrentUser;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowBoard.Api.Services.Authentication;

public class AuthService : IAuthService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly ICurrentUserService _currentUserService;

    public AuthService(
        FlowBoardDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtSettings = jwtSettings.Value;
        _currentUserService = currentUserService;
    }

    // ============================================================
    // REGISTER
    // ============================================================

    public async Task<AuthenticationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email
            .Trim()
            .ToLowerInvariant();

        // Check whether the email already exists.
        var existingUser = await _dbContext.Users
            .AnyAsync(
                x => x.Email == normalizedEmail,
                cancellationToken);

        if (existingUser)
        {
            throw new ConflictException(
                "A user with this email already exists.");
        }

        // Validate password strength.
        if (!PasswordPolicy.IsStrong(request.Password))
        {
            throw new ValidationException(
                "Password must contain at least one uppercase letter, " +
                "one lowercase letter, one number, and one special character.");
        }

        // Create user.
        var user = new User
        {
            Id = Guid.NewGuid(),

            Email = normalizedEmail,

            PasswordHash = _passwordHasher.Hash(
                request.Password),

            FirstName = request.FirstName.Trim(),

            LastName = request.LastName.Trim(),

            Role = UserRole.Employee
        };

        _dbContext.Users.Add(user);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        // Automatically log the user in after registration.
        return await CreateAuthenticationResultAsync(
            user,
            cancellationToken);
    }

    // ============================================================
    // LOGIN
    // ============================================================

    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email
            .Trim()
            .ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Email == normalizedEmail,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException(
                "Invalid email or password.");
        }

        var passwordValid = _passwordHasher.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordValid)
        {
            throw new UnauthorizedException(
                "Invalid email or password.");
        }

        return await CreateAuthenticationResultAsync(
            user,
            cancellationToken);
    }

    // ============================================================
    // REFRESH TOKEN
    // ============================================================

    public async Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException(
                "Refresh token is required.");
        }

        // Hash the raw token from the cookie.
        var tokenHash = HashRefreshToken(
            refreshToken);

        // Find the stored hashed token.
        var storedToken = await _dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);

        if (storedToken is null)
        {
            throw new UnauthorizedException(
                "Invalid refresh token.");
        }

        // Check expiration/revocation.
        if (!storedToken.IsActive)
        {
            throw new UnauthorizedException(
                "Refresh token is expired or revoked.");
        }

        var consumedAt = DateTime.UtcNow;
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var consumedTokenCount = await _dbContext.RefreshTokens
            .Where(token =>
                token.Id == storedToken.Id &&
                token.RevokedAt == null &&
                token.ExpiresAt > consumedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.RevokedAt,
                    (DateTime?)consumedAt),
                cancellationToken);

        if (consumedTokenCount != 1)
        {
            throw new UnauthorizedException(
                "Refresh token is expired or revoked.");
        }

        var user = storedToken.User;
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(newRefreshToken),
            ExpiresAt = consumedAt.AddDays(
                _jwtSettings.RefreshTokenExpirationDays)
        };

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);

        var newAccessToken = _tokenService.GenerateAccessToken(
            user.Id,
            user.Email,
            user.Role.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AuthenticationResult
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = consumedAt.AddMinutes(
                _jwtSettings.AccessTokenExpirationMinutes)
        };
    }

    // ============================================================
    // LOGOUT
    // ============================================================

    public async Task LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        // Hash the token received from the cookie.
        var tokenHash = HashRefreshToken(
            refreshToken);

        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);

        // Token doesn't exist.
        if (storedToken is null)
        {
            return;
        }

        // Already revoked.
        if (storedToken.RevokedAt.HasValue)
        {
            return;
        }

        // Revoke token.
        storedToken.RevokedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    // ============================================================
    // GET PROFILE
    // ============================================================

    public async Task<ProfileResponse?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        return new ProfileResponse
        {
            Id = user.Id,

            FirstName = user.FirstName,

            LastName = user.LastName,

            Email = user.Email,

            Role = user.Role.ToString()
        };
    }

    public async Task<PagedResponse<UserDirectoryResponse>> GetUsersAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var usersQuery = _dbContext.Users.AsNoTracking();
        var isAdmin = string.Equals(
            _currentUserService.Role,
            nameof(UserRole.Admin),
            StringComparison.Ordinal);
        var isManager = string.Equals(
            _currentUserService.Role,
            nameof(UserRole.Manager),
            StringComparison.Ordinal);

        if (!isAdmin && !isManager)
        {
            throw new ForbiddenException(
                "Only administrators and Managers may view the user directory.");
        }

        if (isManager)
        {
            var managerId = _currentUserService.UserId;
            usersQuery = usersQuery.Where(user =>
                user.Role == UserRole.Employee &&
                _dbContext.ManagerEmployees.Any(
                    relationship => relationship.ManagerId == managerId &&
                                    relationship.EmployeeId == user.Id));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            usersQuery = usersQuery.Where(user =>
                user.FirstName.ToLower().Contains(search) ||
                user.LastName.ToLower().Contains(search) ||
                user.Email.ToLower().Contains(search));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var users = await usersQuery
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .ThenBy(user => user.Email)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(user => new UserDirectoryResponse
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<UserDirectoryResponse>
        {
            Items = users,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                (double)totalCount / request.PageSize)
        };
    }

    // ============================================================
    // UPDATE PROFILE
    // ============================================================

    public async Task<ProfileResponse?> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.FirstName = request.FirstName.Trim();

        user.LastName = request.LastName.Trim();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return new ProfileResponse
        {
            Id = user.Id,

            FirstName = user.FirstName,

            LastName = user.LastName,

            Email = user.Email,

            Role = user.Role.ToString()
        };
    }

    // ============================================================
    // CREATE ACCESS + REFRESH TOKEN
    // ============================================================

    private async Task<AuthenticationResult>
        CreateAuthenticationResultAsync(
            User user,
            CancellationToken cancellationToken)
    {
        // Generate access token.
        var accessToken =
            _tokenService.GenerateAccessToken(
                user.Id,
                user.Email,
                user.Role.ToString());

        // Generate refresh token.
        var refreshToken =
            _tokenService.GenerateRefreshToken();

        // We NEVER store the raw refresh token.
        // Only its SHA256 hash is stored.
        var refreshTokenHash =
            HashRefreshToken(refreshToken);

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),

            UserId = user.Id,

            TokenHash = refreshTokenHash,

            ExpiresAt = DateTime.UtcNow.AddDays(
                _jwtSettings.RefreshTokenExpirationDays)
        };

        _dbContext.RefreshTokens.Add(
            refreshTokenEntity);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return new AuthenticationResult
        {
            AccessToken = accessToken,

            RefreshToken = refreshToken,

            ExpiresAt = DateTime.UtcNow.AddMinutes(
                _jwtSettings.AccessTokenExpirationMinutes)
        };
    }

    // ============================================================
    // HASH REFRESH TOKEN
    // ============================================================

    private static string HashRefreshToken(
        string refreshToken)
    {
        var bytes = Encoding.UTF8.GetBytes(
            refreshToken);

        var hash = SHA256.HashData(bytes);

        return Convert.ToBase64String(hash);
    }
}