using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Projects;
using FlowBoard.Api.Services.TaskAssignments;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Data;
using DomainProjectStatus = FlowBoard.Domain.Enums.ProjectStatus;

namespace FlowBoard.Api.Services.Projects;

public class ProjectService : IProjectService
{
    private static readonly Expression<Func<Project, ProjectProjection>>
        ProjectResponseProjection = project => new ProjectProjection
        {
            ProjectId = project.Id,
            Name = project.Name,
            Description = project.Description,
            WorkspaceId = project.WorkspaceId,
            WorkspaceName = project.Workspace.Name,
            ManagerId = project.ManagerId,
            Manager = new UserDirectoryResponse
            {
                UserId = project.Manager.Id,
                Email = project.Manager.Email,
                FirstName = project.Manager.FirstName,
                LastName = project.Manager.LastName
            },
            Status = project.Status,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            CreatedAt = project.CreatedAt,
            ModifiedAt = project.ModifiedAt
        };

    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITaskAssignmentGuard _taskAssignmentGuard;

    public ProjectService(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService,
        ITaskAssignmentGuard taskAssignmentGuard)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _taskAssignmentGuard = taskAssignmentGuard;
    }

    public async Task<ProjectResponse> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectFields(
            request.Name,
            request.StartDate,
            request.EndDate);

        var workspace = await GetWorkspaceAsync(
            request.WorkspaceId,
            cancellationToken);

        Guid managerId;
        if (IsCurrentUserAdmin())
        {
            if (!request.ManagerId.HasValue)
            {
                throw new ValidationException(
                    "A ManagerId is required when an administrator creates a project.");
            }

            var manager = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    user => user.Id == request.ManagerId.Value,
                    cancellationToken);

            if (manager is null)
            {
                throw new NotFoundException("Project Manager was not found.");
            }

            if (manager.Role != UserRole.Manager)
            {
                throw new ValidationException(
                    "The selected Project Manager must have the Manager role.");
            }

            var managerHasWorkspaceAccess =
                manager.Id == workspace.OwnerId ||
                await _dbContext.WorkspaceMembers.AnyAsync(
                    member => member.WorkspaceId == workspace.Id &&
                              member.UserId == manager.Id,
                    cancellationToken);

            if (!managerHasWorkspaceAccess)
            {
                throw new ForbiddenException(
                    "The selected Project Manager must have access to the project workspace.");
            }

            managerId = manager.Id;
        }
        else
        {
            await EnsureCurrentUserIsManagerAsync(cancellationToken);
            await EnsureWorkspaceAccessAsync(workspace, cancellationToken);

            var currentUserId = _currentUserService.UserId;
            if (workspace.OwnerId != currentUserId)
            {
                throw new ForbiddenException(
                    "Only the workspace owner can create a project in this workspace.");
            }

            if (request.ManagerId.HasValue &&
                request.ManagerId.Value != currentUserId)
            {
                throw new ForbiddenException(
                    "Managers cannot assign a project to another Manager.");
            }

            managerId = currentUserId;
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = NormalizeDescription(request.Description),
            WorkspaceId = workspace.Id,
            ManagerId = managerId,
            Status = DomainProjectStatus.Active,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetProjectResponseAsync(project.Id, cancellationToken);
    }

    public async Task<PagedResponse<ProjectResponse>> GetAsync(
        ProjectQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        var isAdmin = IsCurrentUserAdmin();
        var isManager = IsCurrentUserManager();
        var query = _dbContext.Projects.AsNoTracking();

        if (!isAdmin)
        {
            query = query.Where(project =>
                project.Workspace.OwnerId == userId ||
                project.Workspace.Members.Any(member => member.UserId == userId));

            if (isManager)
            {
                query = query.Where(project => project.ManagerId == userId);
            }
            else if (IsCurrentUserEmployee())
            {
                query = query.Where(project =>
                    project.Members.Any(member => member.UserId == userId));
            }
            else
            {
                query = query.Where(_ => false);
            }
        }

        query = ApplyProjectFilters(query, request);
        return await GetProjectPageAsync(query, request, cancellationToken);
    }

    public async Task<PagedResponse<ProjectResponse>> GetMineAsync(
        ProjectQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        var query = _dbContext.Projects
            .AsNoTracking()
            .Where(project =>
                project.ManagerId == userId ||
                project.Members.Any(member => member.UserId == userId));

        if (!IsCurrentUserAdmin())
        {
            if (!IsCurrentUserManager() && !IsCurrentUserEmployee())
            {
                query = query.Where(_ => false);
            }

            query = query.Where(project =>
                project.Workspace.OwnerId == userId ||
                project.Workspace.Members.Any(member => member.UserId == userId));
        }

        query = ApplyProjectFilters(query, request);
        return await GetProjectPageAsync(query, request, cancellationToken);
    }

    public async Task<ProjectResponse> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        await EnsureProjectReadAccessAsync(project, cancellationToken);
        return await GetProjectResponseAsync(projectId, cancellationToken);
    }

    public async Task<ProjectResponse> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectFields(
            request.Name,
            request.StartDate,
            request.EndDate);

        var project = await GetProjectAsync(projectId, cancellationToken);
        await EnsureProjectManagementAccessAsync(project, cancellationToken);

        if (!Enum.IsDefined(request.Status))
        {
            throw new ValidationException("The selected project status is invalid.");
        }

        project.Name = request.Name.Trim();
        project.Description = NormalizeDescription(request.Description);
        project.Status = (DomainProjectStatus)request.Status;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetProjectResponseAsync(project.Id, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        await EnsureProjectManagementAccessAsync(project, cancellationToken);

        if (await HasBoardsAsync(projectId, cancellationToken) ||
            await HasProjectMembersAsync(projectId, cancellationToken))
        {
            throw ProjectHasDependenciesConflict();
        }

        _dbContext.Projects.Remove(project);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await HasBoardsAsync(projectId, cancellationToken) ||
                await HasProjectMembersAsync(projectId, cancellationToken))
            {
                _dbContext.Entry(project).State = EntityState.Unchanged;
                throw ProjectHasDependenciesConflict();
            }

            throw;
        }
    }

    public async Task<PagedResponse<ProjectMemberResponse>> GetMembersAsync(
        Guid projectId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        await EnsureProjectReadAccessAsync(project, cancellationToken);

        var query = _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId);

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
            .Select(member => new ProjectMemberResponse
            {
                ProjectId = member.ProjectId,
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

    public async Task<ProjectMemberResponse> AddMemberAsync(
        Guid projectId,
        AddProjectMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        await EnsureProjectManagementAccessAsync(project, cancellationToken);

        var employee = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == request.UserId,
                cancellationToken);

        if (employee is null)
        {
            throw new NotFoundException("Employee was not found.");
        }

        if (employee.Role != UserRole.Employee)
        {
            throw new ValidationException(
                "Only users with the Employee role can be added as project members.");
        }

        await EnsureManagerEmployeeScopeAsync(
            employee.Id,
            cancellationToken);

        if (await IsProjectMemberAsync(projectId, employee.Id, cancellationToken))
        {
            throw ProjectMemberConflict();
        }

        var membership = new ProjectMember
        {
            ProjectId = projectId,
            UserId = employee.Id,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.ProjectMembers.Add(membership);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(membership).State = EntityState.Detached;
            if (await IsProjectMemberAsync(
                    projectId,
                    employee.Id,
                    cancellationToken))
            {
                throw ProjectMemberConflict();
            }

            throw;
        }

        return new ProjectMemberResponse
        {
            ProjectId = membership.ProjectId,
            UserId = membership.UserId,
            JoinedAt = membership.JoinedAt,
            User = MapUser(employee)
        };
    }

    public async Task RemoveMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        await EnsureProjectManagementAccessAsync(project, cancellationToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var membership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(
                member => member.ProjectId == projectId &&
                          member.UserId == userId,
                cancellationToken);

        if (membership is null)
        {
            throw new NotFoundException("Project membership was not found.");
        }

        await EnsureManagerEmployeeScopeAsync(userId, cancellationToken);
        await _taskAssignmentGuard.EnsureProjectMemberCanBeRemovedAsync(
            projectId,
            userId,
            cancellationToken);

        if (await _dbContext.BoardMembers.AnyAsync(
                member => member.UserId == userId &&
                          member.Board.ProjectId == projectId,
                cancellationToken))
        {
            throw new ConflictException(
                "Remove the user's board memberships before removing them from the project.");
        }

        _dbContext.ProjectMembers.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureProjectReadAccessAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (IsCurrentUserAdmin())
        {
            return;
        }

        var workspace = await GetWorkspaceAsync(
            project.WorkspaceId,
            cancellationToken);
        await EnsureWorkspaceAccessAsync(workspace, cancellationToken);

        var userId = _currentUserService.UserId;
        if (IsCurrentUserManager() && project.ManagerId == userId)
        {
            return;
        }

        if (IsCurrentUserEmployee() &&
            await IsProjectMemberAsync(project.Id, userId, cancellationToken))
        {
            return;
        }

        throw new ForbiddenException(
            "You are not authorized to access this project.");
    }

    private async Task EnsureProjectManagementAccessAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (IsCurrentUserAdmin())
        {
            return;
        }

        var workspace = await GetWorkspaceAsync(
            project.WorkspaceId,
            cancellationToken);
        await EnsureWorkspaceAccessAsync(workspace, cancellationToken);
        await EnsureCurrentUserIsManagerAsync(cancellationToken);

        if (project.ManagerId != _currentUserService.UserId)
        {
            throw new ForbiddenException(
                "Only the responsible Project Manager or an administrator can manage this project.");
        }
    }

    private async Task EnsureCurrentUserIsManagerAsync(
        CancellationToken cancellationToken)
    {
        if (!IsCurrentUserManager())
        {
            throw new ForbiddenException(
                "Only a Manager can create or manage projects.");
        }

        var userId = _currentUserService.UserId;
        var isManager = await _dbContext.Users
            .AnyAsync(
                user => user.Id == userId &&
                        user.Role == UserRole.Manager,
                cancellationToken);

        if (!isManager)
        {
            throw new ForbiddenException(
                "The authenticated user is not an active Manager.");
        }
    }

    private async Task EnsureManagerEmployeeScopeAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        if (IsCurrentUserAdmin())
        {
            return;
        }

        var managerId = _currentUserService.UserId;
        var managesEmployee = await _dbContext.ManagerEmployees
            .AnyAsync(
                relationship => relationship.ManagerId == managerId &&
                                relationship.EmployeeId == employeeId,
                cancellationToken);

        if (!managesEmployee)
        {
            throw new ForbiddenException(
                "You may manage project membership only for employees assigned to you.");
        }
    }

    private async Task<Workspace> GetWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == workspaceId,
                cancellationToken);

        return workspace ??
            throw new NotFoundException("Workspace was not found.");
    }

    private async Task EnsureWorkspaceAccessAsync(
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        if (IsCurrentUserAdmin() ||
            workspace.OwnerId == _currentUserService.UserId)
        {
            return;
        }

        var isMember = await _dbContext.WorkspaceMembers
            .AnyAsync(
                member => member.WorkspaceId == workspace.Id &&
                          member.UserId == _currentUserService.UserId,
                cancellationToken);

        if (!isMember)
        {
            throw new ForbiddenException(
                "You are not authorized to access this workspace.");
        }
    }

    private async Task<Project> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(
                item => item.Id == projectId,
                cancellationToken);

        return project ??
            throw new NotFoundException("Project was not found.");
    }

    private async Task<ProjectResponse> GetProjectResponseAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var response = await _dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(ProjectResponseProjection)
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? throw new NotFoundException("Project was not found.")
            : MapProjectResponse(response);
    }

    private async Task<PagedResponse<ProjectResponse>> GetProjectPageAsync(
        IQueryable<Project> query,
        ProjectQueryRequest request,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var projectProjections = await query
            .OrderBy(project => project.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ProjectResponseProjection)
            .ToListAsync(cancellationToken);

        var projects = projectProjections
            .Select(MapProjectResponse)
            .ToList();

        return CreatePage(projects, request, totalCount);
    }

    private static ProjectResponse MapProjectResponse(ProjectProjection project) =>
        new()
        {
            ProjectId = project.ProjectId,
            Name = project.Name,
            Description = project.Description,
            WorkspaceId = project.WorkspaceId,
            WorkspaceName = project.WorkspaceName,
            ManagerId = project.ManagerId,
            Manager = project.Manager,
            Status = Enum.Parse<FlowBoard.Shared.Enums.ProjectStatus>(
                project.Status.ToString(),
                ignoreCase: false),
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            CreatedAt = project.CreatedAt,
            ModifiedAt = project.ModifiedAt
        };

    private IQueryable<Project> ApplyProjectFilters(
        IQueryable<Project> query,
        ProjectQueryRequest request)
    {
        if (request.WorkspaceId.HasValue)
        {
            query = query.Where(
                project => project.WorkspaceId == request.WorkspaceId.Value);
        }

        if (request.Status.HasValue)
        {
            if (!Enum.IsDefined(request.Status.Value))
            {
                throw new ValidationException(
                    "The selected project status is invalid.");
            }

            var status = (DomainProjectStatus)request.Status.Value;
            query = query.Where(project => project.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(project =>
                project.Name.Contains(search) ||
                (project.Description != null &&
                 project.Description.Contains(search)));
        }

        return query;
    }

    private async Task<bool> IsProjectMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ProjectMembers
            .AnyAsync(
                member => member.ProjectId == projectId &&
                          member.UserId == userId,
                cancellationToken);
    }

    private Task<bool> HasBoardsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Boards
            .AnyAsync(board => board.ProjectId == projectId, cancellationToken);
    }

    private Task<bool> HasProjectMembersAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return _dbContext.ProjectMembers
            .AnyAsync(member => member.ProjectId == projectId, cancellationToken);
    }

    private static void ValidateProjectFields(
        string name,
        DateTime startDate,
        DateTime endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Project name is required.");
        }

        if (startDate == default || endDate == default)
        {
            throw new ValidationException(
                "Project start and end dates are required.");
        }

        if (endDate < startDate)
        {
            throw new ValidationException(
                "Project end date must be on or after its start date.");
        }
    }

    private bool IsCurrentUserAdmin() =>
        string.Equals(
            _currentUserService.Role,
            nameof(UserRole.Admin),
            StringComparison.Ordinal);

    private bool IsCurrentUserManager() =>
        string.Equals(
            _currentUserService.Role,
            nameof(UserRole.Manager),
            StringComparison.Ordinal);

    private bool IsCurrentUserEmployee() =>
        string.Equals(
            _currentUserService.Role,
            nameof(UserRole.Employee),
            StringComparison.Ordinal);

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static ConflictException ProjectHasDependenciesConflict() =>
        new("The project cannot be deleted while boards or members are associated with it.");

    private static ConflictException ProjectMemberConflict() =>
        new("The user is already a member of this project.");

    private static UserDirectoryResponse MapUser(User user) =>
        new()
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        };

    private static PagedResponse<T> CreatePage<T>(
        IReadOnlyList<T> items,
        PagedRequest request,
        int totalCount) =>
        new()
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)request.PageSize)
        };

    private sealed class ProjectProjection
    {
        public Guid ProjectId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Description { get; init; }

        public Guid WorkspaceId { get; init; }

        public string WorkspaceName { get; init; } = string.Empty;

        public Guid ManagerId { get; init; }

        public UserDirectoryResponse Manager { get; init; } = new();

        public DomainProjectStatus Status { get; init; }

        public DateTime StartDate { get; init; }

        public DateTime EndDate { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? ModifiedAt { get; init; }
    }
}
