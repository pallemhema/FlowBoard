using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Teams;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FlowBoard.Api.Services.Teams;

public class TeamService : ITeamService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public TeamService(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<TeamResponse> CreateAsync(
        CreateTeamRequest request,
        CancellationToken cancellationToken = default)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = NormalizeDescription(request.Description)
        };

        _dbContext.Teams.Add(team);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapTeam(team, 0);
    }

    public Task<PagedResponse<TeamResponse>> GetAllAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Teams.AsNoTracking();
        return GetTeamsAsync(query, request, cancellationToken);
    }

    public Task<PagedResponse<TeamResponse>> GetMineAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        var query = _dbContext.Teams
            .AsNoTracking()
            .Where(team => team.Members.Any(member => member.UserId == userId));

        return GetTeamsAsync(query, request, cancellationToken);
    }

    public async Task<TeamResponse> GetByIdAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        var isAdmin = string.Equals(
            _currentUserService.Role,
            nameof(UserRole.Admin),
            StringComparison.Ordinal);

        var team = await _dbContext.Teams
            .AsNoTracking()
            .Where(team => team.Id == teamId)
            .Where(team => isAdmin ||
                team.Members.Any(member => member.UserId == userId))
            .Select(team => new TeamResponse
            {
                TeamId = team.Id,
                Name = team.Name,
                Description = team.Description,
                MemberCount = team.Members.Count,
                CreatedAt = team.CreatedAt,
                ModifiedAt = team.ModifiedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return team ?? throw new NotFoundException("Team was not found.");
    }

    public async Task<TeamResponse> UpdateAsync(
        Guid teamId,
        UpdateTeamRequest request,
        CancellationToken cancellationToken = default)
    {
        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(team => team.Id == teamId, cancellationToken);

        if (team is null)
        {
            throw new NotFoundException("Team was not found.");
        }

        team.Name = request.Name.Trim();
        team.Description = NormalizeDescription(request.Description);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapTeam(
            team,
            await _dbContext.TeamMembers.CountAsync(
                member => member.TeamId == teamId,
                cancellationToken));
    }

    public async Task DeleteAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(team => team.Id == teamId, cancellationToken);

        if (team is null)
        {
            throw new NotFoundException("Team was not found.");
        }

        if (await _dbContext.TeamMembers.AnyAsync(
                member => member.TeamId == teamId,
                cancellationToken))
        {
            throw new ConflictException(
                "The team cannot be deleted while members are assigned. Remove its members first.");
        }

        _dbContext.Teams.Remove(team);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PagedResponse<TeamMemberResponse>> GetMembersAsync(
        Guid teamId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureTeamReadAccessAsync(teamId, cancellationToken);

        var query = _dbContext.TeamMembers
            .AsNoTracking()
            .Where(member => member.TeamId == teamId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(member =>
                member.User.FirstName.ToLower().Contains(search) ||
                member.User.LastName.ToLower().Contains(search) ||
                member.User.Email.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var members = await query
            .OrderBy(member => member.User.FirstName)
            .ThenBy(member => member.User.LastName)
            .ThenBy(member => member.User.Email)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(member => new TeamMemberResponse
            {
                TeamId = member.TeamId,
                UserId = member.UserId,
                JoinedAt = member.JoinedAt,
                User = new UserDirectoryResponse
                {
                    UserId = member.User.Id,
                    Email = member.User.Email,
                    FirstName = member.User.FirstName,
                    LastName = member.User.LastName,
                    Role = member.User.Role == UserRole.Manager
                        ? nameof(UserRole.Manager)
                        : member.User.Role == UserRole.Admin
                            ? nameof(UserRole.Admin)
                            : nameof(UserRole.Employee)
                }
            })
            .ToListAsync(cancellationToken);

        return CreatePage(members, request, totalCount);
    }

    public async Task<TeamMemberResponse> AddMemberAsync(
        Guid teamId,
        AddTeamMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var teamExists = await _dbContext.Teams
            .AnyAsync(team => team.Id == teamId, cancellationToken);

        if (!teamExists)
        {
            throw new NotFoundException("Team was not found.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == request.UserId,
                cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User was not found.");
        }

        if (user.Role != UserRole.Employee)
        {
            throw new ValidationException(
                "Only employees can be added to a team.");
        }

        var alreadyMember = await _dbContext.TeamMembers
            .AnyAsync(
                member => member.TeamId == teamId &&
                          member.UserId == request.UserId,
                cancellationToken);

        if (alreadyMember)
        {
            throw new ConflictException(
                "The user is already a member of this team.");
        }

        var membership = new TeamMember
        {
            TeamId = teamId,
            UserId = user.Id,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.TeamMembers.Add(membership);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(membership).State = EntityState.Detached;
            var duplicateExists = await _dbContext.TeamMembers
                .AnyAsync(
                    member => member.TeamId == teamId &&
                              member.UserId == user.Id,
                    cancellationToken);

            if (duplicateExists)
            {
                throw new ConflictException(
                    "The user is already a member of this team.");
            }

            throw;
        }

        return new TeamMemberResponse
        {
            TeamId = membership.TeamId,
            UserId = membership.UserId,
            JoinedAt = membership.JoinedAt,
            User = MapUser(user)
        };
    }

    public async Task RemoveMemberAsync(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _dbContext.TeamMembers
            .FirstOrDefaultAsync(
                member => member.TeamId == teamId &&
                          member.UserId == userId,
                cancellationToken);

        if (membership is null)
        {
            var teamExists = await _dbContext.Teams
                .AnyAsync(team => team.Id == teamId, cancellationToken);

            if (!teamExists)
            {
                throw new NotFoundException("Team was not found.");
            }

            throw new NotFoundException("Team membership was not found.");
        }

        _dbContext.TeamMembers.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTeamReadAccessAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var isAdmin = string.Equals(
            _currentUserService.Role,
            nameof(UserRole.Admin),
            StringComparison.Ordinal);

        var canRead = await _dbContext.Teams
            .AnyAsync(
                team => team.Id == teamId &&
                    (isAdmin ||
                     team.Members.Any(member => member.UserId == userId)),
                cancellationToken);

        if (!canRead)
        {
            throw new NotFoundException("Team was not found.");
        }
    }

    private static async Task<PagedResponse<TeamResponse>> GetTeamsAsync(
        IQueryable<Team> query,
        PagedSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(team =>
                team.Name.Contains(search) ||
                (team.Description != null &&
                 team.Description.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(team => team.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(team => new TeamResponse
            {
                TeamId = team.Id,
                Name = team.Name,
                Description = team.Description,
                MemberCount = team.Members.Count,
                CreatedAt = team.CreatedAt,
                ModifiedAt = team.ModifiedAt
            })
            .ToListAsync(cancellationToken);

        return CreatePage(items, request, totalCount);
    }

    private static PagedResponse<T> CreatePage<T>(
        IReadOnlyList<T> items,
        PagedRequest request,
        int totalCount)
    {
        return new PagedResponse<T>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)request.PageSize)
        };
    }

    private static TeamResponse MapTeam(Team team, int memberCount)
    {
        return new TeamResponse
        {
            TeamId = team.Id,
            Name = team.Name,
            Description = team.Description,
            MemberCount = memberCount,
            CreatedAt = team.CreatedAt,
            ModifiedAt = team.ModifiedAt
        };
    }

    private static UserDirectoryResponse MapUser(User user)
    {
        return new UserDirectoryResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString()
        };
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
    }
}
