using FlowBoard.Api.Exceptions;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Workspaces;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Auth;

using Microsoft.EntityFrameworkCore;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Api.Services.TaskAssignments;
using System.Data;
namespace FlowBoard.Api.Services.Workspaces;

public class WorkspaceService : IWorkspaceService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITaskAssignmentGuard _taskAssignmentGuard;

    public WorkspaceService(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService,
        ITaskAssignmentGuard taskAssignmentGuard)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _taskAssignmentGuard = taskAssignmentGuard;
    }

    public async Task<WorkspaceResponse> CreateAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentUserAdmin() && !IsCurrentUserManager())
        {
            throw new ForbiddenException(
                "Only an administrator or Manager can create a workspace.");
        }

        var userId = _currentUserService.UserId;

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            OwnerId = userId
        };

        _dbContext.Workspaces.Add(workspace);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetWorkspaceResponseAsync(workspace.Id, cancellationToken);
    }

    public async Task<PagedResponse<WorkspaceResponse>> GetAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        var isAdmin = IsCurrentUserAdmin();
        var isManager = IsCurrentUserManager();

        var query = _dbContext.Workspaces
            .AsNoTracking()
            .Where(workspace =>
                isAdmin ||
                (isManager && workspace.OwnerId == currentUserId) ||
                workspace.Members.Any(member => member.UserId == currentUserId));

        // ============================================================
        // SEARCH
        // ============================================================

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(x =>
                x.Name.Contains(search) ||
                (x.Description != null &&
                 x.Description.Contains(search)));
        }

        // ============================================================
        // TOTAL COUNT
        // ============================================================

        var totalCount = await query.CountAsync(
            cancellationToken);

        // ============================================================
        // TOTAL PAGES
        // ============================================================

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)request.PageSize);

        // ============================================================
        // PAGINATION
        // ============================================================

        var workspaces = await query
            .OrderBy(x => x.Name)
            .Skip(
                (request.Page - 1) *
                request.PageSize)
            .Take(request.PageSize)
            .Select(workspace => new
            {
                Workspace = workspace,
                OwnerName = (workspace.Owner.FirstName + " " +
                             workspace.Owner.LastName).Trim(),
                OwnerEmail = workspace.Owner.Email
            })
            .ToListAsync(cancellationToken);

        // ============================================================
        // RESPONSE
        // ============================================================

        return new PagedResponse<WorkspaceResponse>
        {
            Items = workspaces
                .Select(item => MapToResponse(
                    item.Workspace,
                    item.OwnerName,
                    item.OwnerEmail))
                .ToList(),

            Page = request.Page,

            PageSize = request.PageSize,

            TotalCount = totalCount,

            TotalPages = totalPages
        };
    }

    public async Task<WorkspaceResponse> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var workspaceData = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(x => x.Id == workspaceId)
            .Select(workspace => new
            {
                Workspace = workspace,
                OwnerName = (workspace.Owner.FirstName + " " +
                             workspace.Owner.LastName).Trim(),
                OwnerEmail = workspace.Owner.Email
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspaceData is null)
        {
            throw new NotFoundException(
                "Workspace was not found.");
        }

        await EnsureReadAccessAsync(workspaceData.Workspace, cancellationToken);
        return MapToResponse(
            workspaceData.Workspace,
            workspaceData.OwnerName,
            workspaceData.OwnerEmail);
    }

    public async Task<WorkspaceResponse> UpdateAsync(
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(
                x => x.Id == workspaceId,
                cancellationToken);

        if (workspace is null)
        {
            throw new NotFoundException(
                "Workspace was not found.");
        }

        EnsureOwnerOrAdmin(workspace);

        workspace.Name = request.Name.Trim();
        workspace.Description = request.Description?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetWorkspaceResponseAsync(workspace.Id, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(
                x => x.Id == workspaceId,
                cancellationToken);

        if (workspace is null)
        {
            throw new NotFoundException(
                "Workspace was not found.");
        }

        EnsureOwnerOrAdmin(workspace);

        if (await _dbContext.Projects.AnyAsync(
                project => project.WorkspaceId == workspaceId,
                cancellationToken) ||
            await _dbContext.Boards.AnyAsync(
                board => board.WorkspaceId == workspaceId,
                cancellationToken) ||
            await _dbContext.WorkspaceMembers.AnyAsync(
                member => member.WorkspaceId == workspaceId,
                cancellationToken))
        {
            throw new ConflictException(
                "The workspace cannot be deleted while projects, boards, or members are associated with it. Remove or relocate those relationships first.");
        }

        _dbContext.Workspaces.Remove(workspace);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<WorkspaceMemberResponse>> GetMembersAsync(
        Guid workspaceId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await GetWorkspaceAsync(workspaceId, cancellationToken);
        await EnsureReadAccessAsync(workspace, cancellationToken);

        var query = _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.WorkspaceId == workspaceId);

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
            .Select(member => new WorkspaceMemberResponse
            {
                WorkspaceId = member.WorkspaceId,
                UserId = member.UserId,
                JoinedAt = member.JoinedAt,
                User = new UserDirectoryResponse
                {
                    UserId = member.User.Id,
                    Email = member.User.Email,
                    FirstName = member.User.FirstName,
                    LastName = member.User.LastName
                }
            })
            .ToListAsync(cancellationToken);

        return CreatePage(members, request, totalCount);
    }

    public async Task<WorkspaceMemberResponse> AddMemberAsync(
        Guid workspaceId,
        AddWorkspaceMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await GetWorkspaceAsync(workspaceId, cancellationToken);
        EnsureOwnerOrAdmin(workspace);

        if (request.UserId == workspace.OwnerId)
        {
            throw new ConflictException(
                "The workspace owner is not added as a workspace member.");
        }

        var targetUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == request.UserId,
                cancellationToken);

        if (targetUser is null)
        {
            throw new NotFoundException("User was not found.");
        }

        var actingUserId = _currentUserService.UserId;
        var actingOwner = workspace.OwnerId == actingUserId;
        if (actingOwner &&
            IsCurrentUserManager() &&
            (targetUser.Role != UserRole.Employee ||
             !await _dbContext.ManagerEmployees.AnyAsync(
                 relationship => relationship.ManagerId == actingUserId &&
                                 relationship.EmployeeId == targetUser.Id,
                 cancellationToken)))
        {
            throw new ForbiddenException(
                "A Manager who owns a workspace may add only employees they manage.");
        }

        var existingMembership = await _dbContext.WorkspaceMembers
            .AnyAsync(
                member => member.WorkspaceId == workspaceId &&
                          member.UserId == targetUser.Id,
                cancellationToken);

        if (existingMembership)
        {
            throw new ConflictException(
                "The user is already a member of this workspace.");
        }

        var membership = new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = targetUser.Id,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.WorkspaceMembers.Add(membership);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(membership).State = EntityState.Detached;
            var duplicateExists = await _dbContext.WorkspaceMembers
                .AnyAsync(
                    member => member.WorkspaceId == workspaceId &&
                              member.UserId == targetUser.Id,
                    cancellationToken);

            if (duplicateExists)
            {
                throw new ConflictException(
                    "The user is already a member of this workspace.");
            }

            throw;
        }

        return new WorkspaceMemberResponse
        {
            WorkspaceId = membership.WorkspaceId,
            UserId = membership.UserId,
            JoinedAt = membership.JoinedAt,
            User = MapUser(targetUser)
        };
    }

    public async Task RemoveMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await GetWorkspaceAsync(workspaceId, cancellationToken);
        EnsureOwnerOrAdmin(workspace);

        if (userId == workspace.OwnerId)
        {
            throw new ConflictException(
                "The workspace owner cannot be removed through membership management.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var membership = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(
                member => member.WorkspaceId == workspaceId &&
                          member.UserId == userId,
                cancellationToken);

        if (membership is null)
        {
            throw new NotFoundException(
                "Workspace membership was not found.");
        }

        if (!IsCurrentUserAdmin())
        {
            var targetUserRole = await _dbContext.Users
                .Where(user => user.Id == userId)
                .Select(user => (UserRole?)user.Role)
                .FirstOrDefaultAsync(cancellationToken);

            if (targetUserRole != UserRole.Employee ||
                !await _dbContext.ManagerEmployees.AnyAsync(
                    relationship => relationship.ManagerId == _currentUserService.UserId &&
                                    relationship.EmployeeId == userId,
                    cancellationToken))
            {
                throw new ForbiddenException(
                    "Managers may remove only Employees assigned to them from their workspaces.");
            }
        }

        await _taskAssignmentGuard.EnsureWorkspaceMemberCanBeRemovedAsync(
            workspaceId,
            userId,
            cancellationToken);

        if (await _dbContext.BoardMembers.AnyAsync(
                member => member.UserId == userId &&
                          member.Board.WorkspaceId == workspaceId,
                cancellationToken))
        {
            throw new ConflictException(
                "Remove the user's board memberships before removing them from the workspace.");
        }

        _dbContext.WorkspaceMembers.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<Workspace> GetWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(
                item => item.Id == workspaceId,
                cancellationToken);

        return workspace ??
            throw new NotFoundException("Workspace was not found.");
    }

    private async Task EnsureReadAccessAsync(
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        if (IsCurrentUserAdmin())
        {
            return;
        }

        var currentUserId = _currentUserService.UserId;
        if (IsCurrentUserManager() && workspace.OwnerId == currentUserId)
        {
            return;
        }

        var isMember = await _dbContext.WorkspaceMembers
            .AnyAsync(
                member => member.WorkspaceId == workspace.Id &&
                          member.UserId == currentUserId,
                cancellationToken);

        if (!isMember)
        {
            throw new ForbiddenException(
                "You are not authorized to access this workspace.");
        }
    }

    private void EnsureOwnerOrAdmin(Workspace workspace)
    {
        if (IsCurrentUserAdmin())
        {
            return;
        }

        if (!IsCurrentUserManager() ||
            workspace.OwnerId != _currentUserService.UserId)
        {
            throw new ForbiddenException(
                "Only an administrator or the owning Manager can manage this workspace.");
        }
    }

    private bool IsCurrentUserAdmin()
    {
        return string.Equals(
            _currentUserService.Role,
            nameof(UserRole.Admin),
            StringComparison.Ordinal);
    }

    private bool IsCurrentUserManager()
    {
        return string.Equals(
            _currentUserService.Role,
            nameof(UserRole.Manager),
            StringComparison.Ordinal);
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

    private static UserDirectoryResponse MapUser(User user)
    {
        return new UserDirectoryResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }

    private IQueryable<WorkspaceResponse> GetWorkspaceResponseQuery() =>
        _dbContext.Workspaces
            .AsNoTracking()
            .Select(workspace => new WorkspaceResponse
            {
                Id = workspace.Id,
                Name = workspace.Name,
                Description = workspace.Description,
                OwnerId = workspace.OwnerId,
                OwnerName = (workspace.Owner.FirstName + " " +
                             workspace.Owner.LastName).Trim(),
                OwnerEmail = workspace.Owner.Email,
                CreatedAt = workspace.CreatedAt,
                ModifiedAt = workspace.ModifiedAt
            });

    private async Task<WorkspaceResponse> GetWorkspaceResponseAsync(
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        await GetWorkspaceResponseQuery()
            .FirstAsync(workspace => workspace.Id == workspaceId, cancellationToken);

    private static WorkspaceResponse MapToResponse(
        Workspace workspace,
        string? ownerName,
        string? ownerEmail)
    {
        return new WorkspaceResponse
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Description = workspace.Description,
            OwnerId = workspace.OwnerId,
            OwnerName = ownerName,
            OwnerEmail = ownerEmail,
            CreatedAt = workspace.CreatedAt,
            ModifiedAt = workspace.ModifiedAt
        };
    }
}