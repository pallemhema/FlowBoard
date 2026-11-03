using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FlowBoard.Api.Authorization;
using FlowBoard.Api.Configuration;
using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.Authentication;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Auth;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowBoard.Tests;

public sealed class UserProvisioningTests
{
    [Theory]
    [InlineData("Employee", UserRole.Employee)]
    [InlineData("Manager", UserRole.Manager)]
    public async Task AdminCanCreateEmployeeOrManagerWithHashedSingleUseInvitation(
        string roleName,
        UserRole expectedRole)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);

        var result = await service.CreateUserAsync(new CreateUserRequest
        {
            FirstName = "Sam",
            LastName = "Example",
            Email = " SAM@EXAMPLE.TEST ",
            Role = roleName
        });

        var user = await context.Users.SingleAsync(
            candidate => candidate.Id == result.User.UserId);
        var invitation = await context.UserInvitations.AsNoTracking().SingleAsync(
            candidate => candidate.UserId == user.Id);

        Assert.Equal("sam@example.test", user.Email);
        Assert.Equal(expectedRole, user.Role);
        Assert.Equal(database.CurrentUser.UserId, user.CreatedByUserId);
        Assert.NotEqual(default, user.CreatedAt);
        Assert.NotEqual(result.InvitationToken, user.PasswordHash);
        Assert.False(new PasswordHasher().Verify(
            "StrongPassword1!",
            user.PasswordHash));
        Assert.Equal(64, invitation.TokenHash.Length);
        Assert.NotEqual(result.InvitationToken, invitation.TokenHash);
        Assert.Equal("Invitation pending", result.User.AccountStatus);
        Assert.InRange(
            invitation.ExpiresAt,
            DateTime.UtcNow.AddHours(23),
            DateTime.UtcNow.AddHours(25));
        Assert.Empty(await context.RefreshTokens.ToListAsync());
        Assert.Empty(await context.TeamMembers.ToListAsync());
        Assert.Empty(await context.ManagerEmployees.ToListAsync());
        Assert.Empty(await context.WorkspaceMembers.ToListAsync());
        Assert.Empty(await context.ProjectMembers.ToListAsync());
        Assert.Empty(await context.BoardMembers.ToListAsync());
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.LoginAsync(new LoginRequest
            {
                Email = user.Email,
                Password = "StrongPassword1!"
            }));
    }

    [Fact]
    public async Task AdminDirectoryIncludesAllRolesAndPendingActivationStatus()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);
        var created = await service.CreateUserAsync(CreateRequest());
        var deactivatedUser = CreateUser(
            Guid.NewGuid(),
            "deactivated@example.test",
            UserRole.Employee);
        deactivatedUser.IsActive = false;
        context.Users.Add(deactivatedUser);
        await context.SaveChangesAsync();

        var page = await service.GetUsersAsync(
            new FlowBoard.Shared.DTOs.Common.PagedSearchRequest
            {
                Page = 1,
                PageSize = 10
            });
        var returnedUser = Assert.Single(
            page.Items.Where(user => user.UserId == created.User.UserId));
        var serialized = JsonSerializer.Serialize(returnedUser);

        Assert.Contains(
            page.Items,
            user => user.UserId == database.CurrentUser.UserId &&
                    user.Role == nameof(UserRole.Admin));
        Assert.Equal("Employee", returnedUser.Role);
        Assert.Equal("Invitation pending", returnedUser.AccountStatus);
        Assert.Contains(
            page.Items,
            user => user.UserId == deactivatedUser.Id &&
                    user.AccountStatus == "Deactivated");
        Assert.DoesNotContain("PasswordHash", serialized);
        Assert.DoesNotContain("InvitationToken", serialized);
    }

    [Fact]
    public async Task AdminDeactivationRevokesSessionsAndInvitationsButPreservesRelationships()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var user = CreateUser(
            Guid.NewGuid(),
            "employee@example.test",
            UserRole.Employee);
        var team = new Team { Id = Guid.NewGuid(), Name = "Team" };
        context.Users.Add(user);
        context.Teams.Add(team);
        context.TeamMembers.Add(new TeamMember
        {
            Team = team,
            User = user,
            JoinedAt = DateTime.UtcNow
        });
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            User = user,
            TokenHash = "refresh-token-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        context.UserInvitations.Add(new UserInvitation
        {
            Id = Guid.NewGuid(),
            User = user,
            TokenHash = HashToken("invitation-token"),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, database.CurrentUser);
        await service.DeactivateUserAsync(user.Id);

        Assert.False(await context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == user.Id)
            .Select(candidate => candidate.IsActive)
            .SingleAsync());
        Assert.NotNull((await context.RefreshTokens
            .AsNoTracking()
            .SingleAsync(token => token.UserId == user.Id)).RevokedAt);
        Assert.NotNull((await context.UserInvitations
            .AsNoTracking()
            .SingleAsync(invitation => invitation.UserId == user.Id)).RevokedAt);
        Assert.Single(await context.TeamMembers
            .Where(member => member.UserId == user.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task AdminCanReactivateUserWithoutRestoringRevokedTokens()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var user = CreateUser(
            Guid.NewGuid(),
            "employee@example.test",
            UserRole.Employee);
        user.IsActive = false;
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            User = user,
            TokenHash = "revoked-token-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        var service = CreateService(context, database.CurrentUser);
        await service.ReactivateUserAsync(user.Id);

        Assert.True(await context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == user.Id)
            .Select(candidate => candidate.IsActive)
            .SingleAsync());
        Assert.NotNull((await context.RefreshTokens
            .AsNoTracking()
            .SingleAsync(token => token.UserId == user.Id)).RevokedAt);
    }

    [Fact]
    public async Task AdminCannotDeactivateSelfOrLastActiveAdmin()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.DeactivateUserAsync(database.CurrentUser.UserId));
        await Assert.ThrowsAsync<ConflictException>(
            () => service.ReactivateUserAsync(database.CurrentUser.UserId));

        var remainingAdmin = CreateUser(
            Guid.NewGuid(),
            "last-admin@example.test",
            UserRole.Admin);
        var disabledAdmin = await context.Users.SingleAsync(
            user => user.Id == database.CurrentUser.UserId);
        disabledAdmin.IsActive = false;
        context.Users.Add(remainingAdmin);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(
            () => service.DeactivateUserAsync(remainingAdmin.Id));
        Assert.True((await context.Users.AsNoTracking()
            .SingleAsync(user => user.Id == remainingAdmin.Id)).IsActive);
    }

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Employee)]
    public async Task NonAdminCannotDeactivateOrReactivateUsers(UserRole role)
    {
        await using var database = await TestDatabase.CreateAsync(role);
        await using var context = database.CreateContext();
        var target = CreateUser(
            Guid.NewGuid(),
            "target@example.test",
            UserRole.Employee);
        target.IsActive = false;
        context.Users.Add(target);
        await context.SaveChangesAsync();
        var service = CreateService(context, database.CurrentUser);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.DeactivateUserAsync(target.Id));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.ReactivateUserAsync(target.Id));
    }

    [Fact]
    public async Task DeactivatedUsersCannotLoginOrRefresh()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        const string password = "StrongPassword1!";
        const string refreshTokenValue = "deactivated-user-refresh-token";
        var user = CreateUser(
            Guid.NewGuid(),
            "deactivated@example.test",
            UserRole.Employee);
        user.IsActive = false;
        user.PasswordHash = new PasswordHasher().Hash(password);
        context.Users.Add(user);
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            User = user,
            TokenHash = HashRefreshToken(refreshTokenValue),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        await context.SaveChangesAsync();
        var service = CreateService(context, database.CurrentUser);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.LoginAsync(new LoginRequest
            {
                Email = user.Email,
                Password = password
            }));
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.RefreshTokenAsync(refreshTokenValue));

        Assert.Single(await context.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task ActiveLoginAndRefreshStillIssueRotatedTokens()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        const string password = "StrongPassword1!";
        var user = CreateUser(
            Guid.NewGuid(),
            "active@example.test",
            UserRole.Employee);
        user.PasswordHash = new PasswordHasher().Hash(password);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new AuthService(
            context,
            new PasswordHasher(),
            new TestTokenService(),
            Options.Create(new JwtSettings
            {
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30
            }),
            database.CurrentUser);

        var login = await service.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = password
        });
        var refresh = await service.RefreshTokenAsync(login.RefreshToken);

        Assert.NotEqual(login.AccessToken, refresh.AccessToken);
        Assert.NotEqual(login.RefreshToken, refresh.RefreshToken);
        Assert.Equal(2, await context.RefreshTokens.CountAsync(
            token => token.UserId == user.Id));
        Assert.Single(await context.RefreshTokens.Where(
            token => token.UserId == user.Id &&
                     token.RevokedAt == null).ToListAsync());
    }

    [Fact]
    public void CreateUserRequestValidatesNamesAndEmail()
    {
        var request = new CreateUserRequest
        {
            FirstName = "A",
            LastName = "Example1",
            Email = "not-an-email",
            Role = "Admin"
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(nameof(request.FirstName)));
        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(nameof(request.LastName)));
        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(nameof(request.Email)));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("User")]
    [InlineData("admin")]
    [InlineData("Unexpected")]
    public async Task AdminCannotCreateUnsupportedRole(string role)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);

        await Assert.ThrowsAsync<FlowBoard.Api.Exceptions.ValidationException>(
            () => service.CreateUserAsync(new CreateUserRequest
            {
                FirstName = "Sam",
                LastName = "Example",
                Email = "sam@example.test",
                Role = role
            }));

        Assert.Equal(1, await context.Users.CountAsync());
    }

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Employee)]
    public async Task NonAdminCannotCreateAccounts(UserRole role)
    {
        await using var database = await TestDatabase.CreateAsync(role);
        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.CreateUserAsync(new CreateUserRequest
            {
                FirstName = "Sam",
                LastName = "Example",
                Email = "sam@example.test",
                Role = "Employee"
            }));

        Assert.Equal(1, await context.Users.CountAsync());
    }

    [Fact]
    public async Task DuplicateEmailIsRejectedWithoutCreatingInvitation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        context.Users.Add(CreateUser(
            Guid.NewGuid(),
            "sam@example.test",
            UserRole.Employee));
        await context.SaveChangesAsync();

        var service = CreateService(context, database.CurrentUser);
        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateUserAsync(new CreateUserRequest
            {
                FirstName = "Another",
                LastName = "Person",
                Email = "SAM@EXAMPLE.TEST",
                Role = "Employee"
            }));

        Assert.Empty(await context.UserInvitations.ToListAsync());
    }

    [Fact]
    public async Task InvitationActivationHashesPasswordAndRejectsReplay()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);
        var created = await service.CreateUserAsync(CreateRequest());
        const string password = "StrongPassword1!";

        await service.ActivateInvitationAsync(new ActivateInvitationRequest
        {
            Token = created.InvitationToken,
            Password = password
        });

        var user = await context.Users.SingleAsync(
            candidate => candidate.Id == created.User.UserId);
        var invitation = await context.UserInvitations.AsNoTracking().SingleAsync(
            candidate => candidate.UserId == user.Id);

        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(new PasswordHasher().Verify(password, user.PasswordHash));
        Assert.NotNull(invitation.ConsumedAt);
        Assert.Equal(UserRole.Employee, user.Role);
        Assert.Empty(await context.RefreshTokens.ToListAsync());
        await Assert.ThrowsAsync<FlowBoard.Api.Exceptions.ValidationException>(
            () => service.ActivateInvitationAsync(new ActivateInvitationRequest
            {
                Token = created.InvitationToken,
                Password = password
            }));
        await Assert.ThrowsAsync<ConflictException>(
            () => service.ReissueInvitationAsync(user.Id));
    }

    [Fact]
    public async Task ExpiredInvitationCannotSetPassword()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);
        var created = await service.CreateUserAsync(CreateRequest());
        var invitation = await context.UserInvitations.SingleAsync(
            candidate => candidate.UserId == created.User.UserId);
        invitation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<FlowBoard.Api.Exceptions.ValidationException>(
            () => service.ActivateInvitationAsync(new ActivateInvitationRequest
            {
                Token = created.InvitationToken,
                Password = "StrongPassword1!"
            }));

        Assert.Null((await context.UserInvitations.SingleAsync()).ConsumedAt);
    }

    [Fact]
    public async Task ReissuedInvitationRevokesPreviousToken()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);
        var created = await service.CreateUserAsync(CreateRequest());
        var replacement = await service.ReissueInvitationAsync(
            created.User.UserId);

        Assert.NotEqual(created.InvitationToken, replacement.InvitationToken);
        Assert.NotNull((await context.UserInvitations.AsNoTracking().SingleAsync(
            invitation => invitation.TokenHash ==
                HashToken(created.InvitationToken))).RevokedAt);

        await Assert.ThrowsAsync<FlowBoard.Api.Exceptions.ValidationException>(
            () => service.ActivateInvitationAsync(new ActivateInvitationRequest
            {
                Token = created.InvitationToken,
                Password = "StrongPassword1!"
            }));

        await service.ActivateInvitationAsync(new ActivateInvitationRequest
        {
            Token = replacement.InvitationToken,
            Password = "StrongPassword1!"
        });
    }

    [Fact]
    public async Task PromotionRemovesOnlyReportingLinksAndAuditsTheRoleChange()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var employee = CreateUser(
            Guid.NewGuid(),
            "employee@example.test",
            UserRole.Employee);
        var manager = CreateUser(
            Guid.NewGuid(),
            "manager@example.test",
            UserRole.Manager);
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Workspace",
            Owner = manager
        };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = "Team"
        };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project",
            Workspace = workspace,
            Manager = manager,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(1)
        };
        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = "Board",
            Owner = manager,
            Workspace = workspace,
            Project = project
        };
        var column = new BoardColumn
        {
            Id = Guid.NewGuid(),
            Name = "To do",
            Board = board
        };
        var card = new Card
        {
            Id = Guid.NewGuid(),
            Title = "Assigned task",
            BoardColumn = column,
            AssignedUser = employee
        };

        context.Users.AddRange(employee, manager);
        context.ManagerEmployees.Add(new ManagerEmployee
        {
            ManagerId = manager.Id,
            EmployeeId = employee.Id,
            CreatedAt = DateTime.UtcNow
        });
        context.TeamMembers.Add(new TeamMember
        {
            Team = team,
            User = employee,
            JoinedAt = DateTime.UtcNow
        });
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            Workspace = workspace,
            User = employee,
            JoinedAt = DateTime.UtcNow
        });
        context.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            User = employee,
            JoinedAt = DateTime.UtcNow
        });
        context.BoardMembers.Add(new BoardMember
        {
            Board = board,
            User = employee,
            Role = BoardMemberRole.Editor,
            JoinedAt = DateTime.UtcNow
        });
        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var service = CreateService(context, database.CurrentUser);
        var result = await service.PromoteEmployeeToManagerAsync(employee.Id);

        Assert.Equal(nameof(UserRole.Employee), result.PreviousRole);
        Assert.Equal(nameof(UserRole.Manager), result.NewRole);
        Assert.Equal(nameof(UserRole.Manager), result.User.Role);
        Assert.Empty(await context.ManagerEmployees.ToListAsync());
        Assert.Single(await context.TeamMembers.ToListAsync());
        Assert.Single(await context.WorkspaceMembers.ToListAsync());
        Assert.Single(await context.ProjectMembers.ToListAsync());
        Assert.Single(await context.BoardMembers.ToListAsync());
        Assert.Equal(employee.Id, (await context.Cards.SingleAsync()).AssignedUserId);

        var audit = await context.UserRoleChangeAudits.SingleAsync();
        Assert.Equal(employee.Id, audit.TargetUserId);
        Assert.Equal(UserRole.Employee, audit.PreviousRole);
        Assert.Equal(UserRole.Manager, audit.NewRole);
        Assert.Equal(database.CurrentUser.UserId, audit.ChangedByUserId);
        Assert.Equal(database.CurrentUser.UserId, audit.CreatedByUserId);
        Assert.Equal(result.ChangedAt, audit.ChangedAt);
    }

    [Fact]
    public async Task FailedPromotionRollsBackRoleAndReportingCleanup()
    {
        await using var database = await TestDatabase.CreateAsync();
        var employeeId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        await using (var seedContext = database.CreateContext())
        {
            var employee = CreateUser(
                employeeId,
                "employee@example.test",
                UserRole.Employee);
            var manager = CreateUser(
                managerId,
                "manager@example.test",
                UserRole.Manager);
            seedContext.Users.AddRange(employee, manager);
            seedContext.ManagerEmployees.Add(new ManagerEmployee
            {
                ManagerId = managerId,
                EmployeeId = employeeId,
                CreatedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
            await seedContext.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER FailRoleChangeAudit " +
                "BEFORE INSERT ON UserRoleChangeAudits " +
                "BEGIN SELECT RAISE(ABORT, 'forced audit failure'); END;");
        }

        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.PromoteEmployeeToManagerAsync(employeeId));

        Assert.Equal(
            UserRole.Employee,
            (await context.Users.SingleAsync(user => user.Id == employeeId)).Role);
        Assert.Single(await context.ManagerEmployees.ToListAsync());
        Assert.Empty(await context.UserRoleChangeAudits.ToListAsync());
    }

    [Fact]
    public async Task AdminCannotPromoteManagerOrAdmin()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var manager = CreateUser(
            Guid.NewGuid(),
            "manager@example.test",
            UserRole.Manager);
        var admin = CreateUser(
            Guid.NewGuid(),
            "second-admin@example.test",
            UserRole.Admin);
        context.Users.AddRange(manager, admin);
        await context.SaveChangesAsync();
        var service = CreateService(context, database.CurrentUser);

        await Assert.ThrowsAsync<FlowBoard.Api.Exceptions.ValidationException>(
            () => service.PromoteEmployeeToManagerAsync(manager.Id));
        await Assert.ThrowsAsync<FlowBoard.Api.Exceptions.ValidationException>(
            () => service.PromoteEmployeeToManagerAsync(admin.Id));

        Assert.Empty(await context.UserRoleChangeAudits.ToListAsync());
    }

    [Fact]
    public async Task NonAdminCannotPromoteEmployee()
    {
        await using var database = await TestDatabase.CreateAsync(UserRole.Manager);
        await using var context = database.CreateContext();
        var employee = CreateUser(
            Guid.NewGuid(),
            "employee@example.test",
            UserRole.Employee);
        context.Users.Add(employee);
        await context.SaveChangesAsync();
        var service = CreateService(context, database.CurrentUser);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.PromoteEmployeeToManagerAsync(employee.Id));

        Assert.Equal(UserRole.Employee, employee.Role);
        Assert.Empty(await context.UserRoleChangeAudits.ToListAsync());
    }

    [Fact]
    public async Task EmployeeCannotPromoteThemself()
    {
        await using var database = await TestDatabase.CreateAsync(
            UserRole.Employee);
        await using var context = database.CreateContext();
        var service = CreateService(context, database.CurrentUser);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.PromoteEmployeeToManagerAsync(
                database.CurrentUser.UserId));

        Assert.Equal(
            UserRole.Employee,
            (await context.Users.SingleAsync(
                user => user.Id == database.CurrentUser.UserId)).Role);
    }

    private static CreateUserRequest CreateRequest() =>
        new()
        {
            FirstName = "Sam",
            LastName = "Example",
            Email = "sam@example.test",
            Role = "Employee"
        };

    private static User CreateUser(
        Guid userId,
        string email,
        UserRole role) =>
        new()
        {
            Id = userId,
            Email = email,
            PasswordHash = "existing-hash",
            FirstName = "Test",
            LastName = "User",
            Role = role
        };

    private static AuthService CreateService(
        FlowBoardDbContext context,
        TestCurrentUser currentUser) =>
        new(
            context,
            new PasswordHasher(),
            new UnusedTokenService(),
            Options.Create(new JwtSettings()),
            currentUser);

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string HashRefreshToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed class TestCurrentUser(
        Guid userId,
        UserRole role) : ICurrentUserService
    {
        public Guid UserId { get; } = userId;

        public string? Email => null;

        public string? Role => role.ToString();

        public bool IsAuthenticated => true;
    }

    private sealed class UnusedTokenService : ITokenService
    {
        public string GenerateAccessToken(
            Guid userId,
            string email,
            string role) =>
            throw new NotSupportedException();

        public string GenerateRefreshToken() =>
            throw new NotSupportedException();
    }

    private sealed class TestTokenService : ITokenService
    {
        private int _tokenNumber;

        public string GenerateAccessToken(
            Guid userId,
            string email,
            string role) =>
            $"access-{Interlocked.Increment(ref _tokenNumber)}";

        public string GenerateRefreshToken() =>
            $"refresh-{Interlocked.Increment(ref _tokenNumber)}";
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _anchorConnection;

        private TestDatabase(
            string connectionString,
            SqliteConnection anchorConnection,
            TestCurrentUser currentUser)
        {
            _connectionString = connectionString;
            _anchorConnection = anchorConnection;
            CurrentUser = currentUser;
        }

        public TestCurrentUser CurrentUser { get; }

        public static async Task<TestDatabase> CreateAsync(
            UserRole currentRole = UserRole.Admin)
        {
            var connectionString =
                new SqliteConnectionStringBuilder
                {
                    DataSource = $"ProvisioningTests_{Guid.NewGuid():N}",
                    Mode = SqliteOpenMode.Memory,
                    Cache = SqliteCacheMode.Shared
                }.ToString();
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync();
            var currentUser = new TestCurrentUser(Guid.NewGuid(), currentRole);
            var database = new TestDatabase(
                connectionString,
                anchorConnection,
                currentUser);

            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            context.Users.Add(CreateUser(
                currentUser.UserId,
                "admin@example.test",
                currentRole));
            await context.SaveChangesAsync();

            return database;
        }

        public FlowBoardDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<FlowBoardDbContext>()
                .UseSqlite(_connectionString)
                .Options;
            return new FlowBoardDbContext(options, CurrentUser);
        }

        public async ValueTask DisposeAsync()
        {
            await _anchorConnection.DisposeAsync();
        }
    }
}
