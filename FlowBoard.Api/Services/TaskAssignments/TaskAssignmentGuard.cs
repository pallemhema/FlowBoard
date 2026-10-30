using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlowBoard.Api.Services.TaskAssignments;

public class TaskAssignmentGuard : ITaskAssignmentGuard
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public TaskAssignmentGuard(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task ValidateAssigneeAsync(
        Guid boardId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == employeeId,
                cancellationToken);

        if (employee is null)
        {
            throw new NotFoundException("Assigned employee was not found.");
        }

        if (employee.Role != UserRole.Employee)
        {
            throw new ValidationException(
                "Only users with the Employee role can be assigned tasks.");
        }

        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == boardId,
                cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board was not found.");
        }

        var isBoardOwner = board.OwnerId == employeeId;
        var isBoardMember = isBoardOwner ||
            await _dbContext.BoardMembers.AnyAsync(
                member => member.BoardId == boardId &&
                          member.UserId == employeeId,
                cancellationToken);

        if (!isBoardMember)
        {
            throw new ConflictException(
                "The assigned employee must be a member of the board.");
        }

        switch (board.Purpose)
        {
            case BoardPurpose.Project:
                if (!board.ProjectId.HasValue)
                {
                    throw new ConflictException(
                        "The Project board is not associated with a project.");
                }

                var projectWorkspaceId = await _dbContext.Projects
                    .Where(project => project.Id == board.ProjectId.Value)
                    .Select(project => (Guid?)project.WorkspaceId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (!projectWorkspaceId.HasValue)
                {
                    throw new ConflictException(
                        "The Project board references a project that does not exist.");
                }

                if (projectWorkspaceId.Value != board.WorkspaceId)
                {
                    throw new ConflictException(
                        "The project and board must belong to the same workspace.");
                }

                var isProjectMember = await _dbContext.ProjectMembers
                    .AnyAsync(
                        member => member.ProjectId == board.ProjectId.Value &&
                                  member.UserId == employeeId,
                        cancellationToken);

                if (!isProjectMember)
                {
                    throw new ConflictException(
                        "The assigned employee must be a member of the board's project.");
                }

                break;

            case BoardPurpose.GeneralLearning:
                var isWorkspaceMember = await _dbContext.WorkspaceMembers
                    .AnyAsync(
                        member => member.WorkspaceId == board.WorkspaceId &&
                                  member.UserId == employeeId,
                        cancellationToken);

                if (!isWorkspaceMember)
                {
                    throw new ConflictException(
                        "The assigned employee must be a member of the board's workspace.");
                }

                break;

            default:
                throw new ConflictException(
                    "The board has an unsupported purpose.");
        }

        if (string.Equals(
                _currentUserService.Role,
                nameof(UserRole.Manager),
                StringComparison.Ordinal))
        {
            if (!await _dbContext.ManagerEmployees.AnyAsync(
                    relationship =>
                        relationship.ManagerId == _currentUserService.UserId &&
                        relationship.EmployeeId == employeeId,
                    cancellationToken))
            {
                throw new ForbiddenException(
                    "Managers may assign tasks only to employees assigned to them.");
            }
        }
        else if (string.Equals(
                     _currentUserService.Role,
                     nameof(UserRole.Employee),
                     StringComparison.Ordinal) &&
                 _currentUserService.UserId != employeeId)
        {
            throw new ForbiddenException(
                "Employees may assign tasks only to themselves.");
        }
        else if (!string.Equals(
                     _currentUserService.Role,
                     nameof(UserRole.Employee),
                     StringComparison.Ordinal) &&
                 !string.Equals(
                     _currentUserService.Role,
                     nameof(UserRole.Admin),
                     StringComparison.Ordinal))
        {
            throw new ForbiddenException(
                "You are not authorized to assign tasks.");
        }
    }

    public async Task EnsureBoardMemberCanBeRemovedAsync(
        Guid boardId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var hasAssignedCards = await _dbContext.Cards
            .AnyAsync(
                card => card.AssignedUserId == userId &&
                        card.BoardColumn.BoardId == boardId,
                cancellationToken);

        EnsureNoAssignments(
            hasAssignedCards,
            "board membership");
    }

    public async Task EnsureProjectMemberCanBeRemovedAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var hasAssignedCards = await _dbContext.Cards
            .AnyAsync(
                card => card.AssignedUserId == userId &&
                        card.BoardColumn.Board.ProjectId == projectId,
                cancellationToken);

        EnsureNoAssignments(
            hasAssignedCards,
            "project membership");
    }

    public async Task EnsureWorkspaceMemberCanBeRemovedAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var hasAssignedCards = await _dbContext.Cards
            .AnyAsync(
                card => card.AssignedUserId == userId &&
                        card.BoardColumn.Board.WorkspaceId == workspaceId &&
                        card.BoardColumn.Board.Purpose ==
                            BoardPurpose.GeneralLearning,
                cancellationToken);

        EnsureNoAssignments(
            hasAssignedCards,
            "workspace membership");
    }

    private static void EnsureNoAssignments(
        bool hasAssignedCards,
        string membershipType)
    {
        if (hasAssignedCards)
        {
            throw new ConflictException(
                $"Cannot remove {membershipType} while the employee has assigned tasks that depend on it. Reassign or unassign those tasks first.");
        }
    }
}
