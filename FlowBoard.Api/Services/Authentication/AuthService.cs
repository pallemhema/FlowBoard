using System.Security.Cryptography;
using System.Text;
using System.Data;
using Microsoft.AspNetCore.WebUtilities;

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
    private static readonly TimeSpan InvitationLifetime =
        TimeSpan.FromHours(24);

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

    public async Task<CreateUserResponse> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();

        var role = request.Role switch
        {
            nameof(UserRole.Employee) => UserRole.Employee,
            nameof(UserRole.Manager) => UserRole.Manager,
            _ => throw new ValidationException(
                "Only Employee and Manager accounts can be created here.")
        };

        var normalizedEmail = request.Email
            .Trim()
            .ToLowerInvariant();

        if (await _dbContext.Users
            .AnyAsync(
                user => user.Email.ToLower() == normalizedEmail,
                cancellationToken))
        {
            throw new ConflictException(
                "A user with this email already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = role
        };

        var (invitation, rawToken) = CreateInvitation(user.Id, now);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            _dbContext.Users.Add(user);
            _dbContext.UserInvitations.Add(invitation);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (await _dbContext.Users.AnyAsync(
                    existing => existing.Email.ToLower() == normalizedEmail,
                    cancellationToken))
            {
                throw new ConflictException(
                    "A user with this email already exists.");
            }

            throw;
        }

        return new CreateUserResponse
        {
            User = MapDirectoryUser(user, "Invitation pending"),
            InvitationToken = rawToken,
            InvitationExpiresAt = invitation.ExpiresAt
        };
    }

    public async Task<CreateUserResponse> ReissueInvitationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                candidate => candidate.Id == userId,
                cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        if (!user.IsActive)
        {
            throw new ConflictException(
                "A deactivated account cannot receive a new invitation.");
        }

        if (user.Role == UserRole.Admin)
        {
            throw new ValidationException(
                "Invitation links cannot be issued for Admin accounts.");
        }

        var hasConsumedInvitation = await _dbContext.UserInvitations
            .AnyAsync(
                invitation => invitation.UserId == userId &&
                              invitation.ConsumedAt != null,
                cancellationToken);
        var hasInvitation = await _dbContext.UserInvitations
            .AnyAsync(
                invitation => invitation.UserId == userId,
                cancellationToken);

        if (hasConsumedInvitation || !hasInvitation)
        {
            throw new ConflictException(
                "This account is already active or has no pending invitation.");
        }

        var now = DateTime.UtcNow;
        await _dbContext.UserInvitations
            .Where(invitation =>
                invitation.UserId == userId &&
                invitation.ConsumedAt == null &&
                invitation.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        invitation => invitation.RevokedAt,
                        (DateTime?)now)
                    .SetProperty(
                        invitation => invitation.ModifiedAt,
                        (DateTime?)now)
                    .SetProperty(
                        invitation => invitation.ModifiedByUserId,
                        (Guid?)_currentUserService.UserId),
                cancellationToken);

        var (invitation, rawToken) = CreateInvitation(userId, now);
        _dbContext.UserInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateUserResponse
        {
            User = MapDirectoryUser(user, "Invitation pending"),
            InvitationToken = rawToken,
            InvitationExpiresAt = invitation.ExpiresAt
        };
    }

    public async Task<PromoteUserResponse> PromoteEmployeeToManagerAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == userId,
                cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        if (user.Role != UserRole.Employee)
        {
            throw new ValidationException(
                "Only Employee accounts can be promoted to Manager.");
        }

        var changedAt = DateTime.UtcNow;
        var updatedCount = await _dbContext.Users
            .Where(candidate =>
                candidate.Id == userId &&
                candidate.Role == UserRole.Employee)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        candidate => candidate.Role,
                        UserRole.Manager)
                    .SetProperty(
                        candidate => candidate.ModifiedAt,
                        (DateTime?)changedAt)
                    .SetProperty(
                        candidate => candidate.ModifiedByUserId,
                        (Guid?)_currentUserService.UserId),
                cancellationToken);

        if (updatedCount != 1)
        {
            throw new ConflictException(
                "The user's role changed before the promotion completed.");
        }

        user.Role = UserRole.Manager;

        await _dbContext.ManagerEmployees
            .Where(relationship => relationship.EmployeeId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.UserRoleChangeAudits.Add(new UserRoleChangeAudit
        {
            Id = Guid.NewGuid(),
            TargetUserId = userId,
            PreviousRole = UserRole.Employee,
            NewRole = UserRole.Manager,
            ChangedByUserId = _currentUserService.UserId,
            ChangedAt = changedAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PromoteUserResponse
        {
            User = MapDirectoryUser(user),
            PreviousRole = nameof(UserRole.Employee),
            NewRole = nameof(UserRole.Manager),
            ChangedAt = changedAt
        };
    }

    public async Task DeactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();

        if (userId == _currentUserService.UserId)
        {
            throw new ConflictException(
                "Administrators cannot deactivate their own account.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == userId,
                cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        if (user.IsActive && user.Role == UserRole.Admin &&
            await _dbContext.Users.CountAsync(
                candidate =>
                    candidate.Role == UserRole.Admin &&
                    candidate.IsActive,
                cancellationToken) <= 1)
        {
            throw new ConflictException(
                "The last active administrator cannot be deactivated.");
        }

        var changedAt = DateTime.UtcNow;
        await _dbContext.Users
            .Where(candidate =>
                candidate.Id == userId &&
                candidate.IsActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.IsActive, false)
                    .SetProperty(
                        candidate => candidate.ModifiedAt,
                        (DateTime?)changedAt)
                    .SetProperty(
                        candidate => candidate.ModifiedByUserId,
                        (Guid?)_currentUserService.UserId),
                cancellationToken);

        await _dbContext.RefreshTokens
            .Where(token =>
                token.UserId == userId &&
                token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        token => token.RevokedAt,
                        (DateTime?)changedAt)
                    .SetProperty(
                        token => token.ModifiedAt,
                        (DateTime?)changedAt)
                    .SetProperty(
                        token => token.ModifiedByUserId,
                        (Guid?)_currentUserService.UserId),
                cancellationToken);

        await _dbContext.UserInvitations
            .Where(invitation =>
                invitation.UserId == userId &&
                invitation.ConsumedAt == null &&
                invitation.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        invitation => invitation.RevokedAt,
                        (DateTime?)changedAt)
                    .SetProperty(
                        invitation => invitation.ModifiedAt,
                        (DateTime?)changedAt)
                    .SetProperty(
                        invitation => invitation.ModifiedByUserId,
                        (Guid?)_currentUserService.UserId),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();

        if (userId == _currentUserService.UserId)
        {
            throw new ConflictException(
                "Administrators cannot reactivate their own account.");
        }

        var changedAt = DateTime.UtcNow;
        var reactivatedCount = await _dbContext.Users
            .Where(candidate =>
                candidate.Id == userId &&
                !candidate.IsActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.IsActive, true)
                    .SetProperty(
                        candidate => candidate.ModifiedAt,
                        (DateTime?)changedAt)
                    .SetProperty(
                        candidate => candidate.ModifiedByUserId,
                        (Guid?)_currentUserService.UserId),
                cancellationToken);

        if (reactivatedCount == 1)
        {
            return;
        }

        if (!await _dbContext.Users.AnyAsync(
                candidate => candidate.Id == userId,
                cancellationToken))
        {
            throw new NotFoundException("User was not found.");
        }

        throw new ConflictException("The account is already active.");
    }

    public async Task ActivateInvitationAsync(
        ActivateInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!PasswordPolicy.IsStrong(request.Password))
        {
            throw new ValidationException(
                "Password must contain at least one uppercase letter, " +
                "one lowercase letter, one number, and one special character.");
        }

        var tokenHash = HashInvitationToken(request.Token);
        var now = DateTime.UtcNow;
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var invitation = await _dbContext.UserInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.TokenHash == tokenHash &&
                    candidate.ConsumedAt == null &&
                    candidate.RevokedAt == null &&
                    candidate.ExpiresAt > now,
                cancellationToken);

        if (invitation is null)
        {
            throw new ValidationException(
                "This invitation is invalid, expired, or has already been used.");
        }

        var claimedCount = await _dbContext.UserInvitations
            .Where(candidate =>
                candidate.Id == invitation.Id &&
                candidate.ConsumedAt == null &&
                candidate.RevokedAt == null &&
                candidate.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.ConsumedAt,
                    (DateTime?)now),
                cancellationToken);

        if (claimedCount != 1)
        {
            throw new ValidationException(
                "This invitation is invalid, expired, or has already been used.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                candidate => candidate.Id == invitation.UserId,
                cancellationToken);
        if (user is null)
        {
            throw new ValidationException(
                "This invitation is invalid, expired, or has already been used.");
        }

        if (!user.IsActive)
        {
            throw new ValidationException(
                "This invitation is invalid, expired, or has already been used.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.Password);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
                AuthErrorMessages.InvalidCredentials);
        }

        var passwordValid = _passwordHasher.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordValid)
        {
            throw new UnauthorizedException(
                AuthErrorMessages.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException(
                AuthErrorMessages.AccountDeactivated);
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

        await EnsureUserActiveForAuthenticationAsync(
            storedToken.UserId,
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
        var refreshTokenExpiresAt = consumedAt.AddDays(
            _jwtSettings.RefreshTokenExpirationDays);
        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(newRefreshToken),
            ExpiresAt = refreshTokenExpiresAt
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
                _jwtSettings.AccessTokenExpirationMinutes),
            RefreshTokenExpiresAt = refreshTokenExpiresAt
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

        return await GetPagedUsersAsync(
            usersQuery,
            request,
            isAdmin,
            cancellationToken);
    }

    public Task<PagedResponse<UserDirectoryResponse>> GetEmployeesAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();

        var employeesQuery = _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Employee);

        return GetPagedUsersAsync(
            employeesQuery,
            request,
            isAdmin: true,
            cancellationToken: cancellationToken);
    }

    private async Task<PagedResponse<UserDirectoryResponse>> GetPagedUsersAsync(
        IQueryable<User> usersQuery,
        PagedSearchRequest request,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
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
                LastName = user.LastName,
                Role = user.Role.ToString(),
                AccountStatus = isAdmin
                    ? (!user.IsActive
                        ? "Deactivated"
                        : (!user.Invitations.Any() ||
                           user.Invitations.Any(
                               invitation => invitation.ConsumedAt != null)
                            ? "Active"
                            : "Invitation pending"))
                    : null
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
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        await EnsureUserActiveForAuthenticationAsync(
            user.Id,
            cancellationToken);

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
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(
            _jwtSettings.RefreshTokenExpirationDays);

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),

            UserId = user.Id,

            TokenHash = refreshTokenHash,

            ExpiresAt = refreshTokenExpiresAt
        };

        _dbContext.RefreshTokens.Add(
            refreshTokenEntity);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AuthenticationResult
        {
            AccessToken = accessToken,

            RefreshToken = refreshToken,

            ExpiresAt = DateTime.UtcNow.AddMinutes(
                _jwtSettings.AccessTokenExpirationMinutes),

            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };
    }

    private async Task EnsureUserActiveForAuthenticationAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var activeUserCount = await _dbContext.Users
            .Where(user => user.Id == userId && user.IsActive)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    user => user.IsActive,
                    user => user.IsActive),
                cancellationToken);

        if (activeUserCount != 1)
        {
            throw new UnauthorizedException(
                "Invalid email or password.");
        }
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

    private void EnsureAdmin()
    {
        if (!string.Equals(
                _currentUserService.Role,
                nameof(UserRole.Admin),
                StringComparison.Ordinal))
        {
            throw new ForbiddenException(
                "Only administrators can manage user accounts.");
        }
    }

    private static (UserInvitation Invitation, string RawToken)
        CreateInvitation(Guid userId, DateTime createdAt)
    {
        var rawToken = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(32));

        return (
            new UserInvitation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = HashInvitationToken(rawToken),
                ExpiresAt = createdAt.Add(InvitationLifetime)
            },
            rawToken);
    }

    private static string HashInvitationToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private static UserDirectoryResponse MapDirectoryUser(
        User user,
        string? accountStatus = null) =>
        new()
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString(),
            AccountStatus = accountStatus ??
                (user.IsActive ? "Active" : "Deactivated")
        };
}