using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlowBoard.Api.Services.BoardAuthorization;

public class BoardAuthorizationService : IBoardAuthorizationService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public BoardAuthorizationService(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public IQueryable<Board> GetAccessibleBoardsQuery()
    {
        var userId = _currentUserService.UserId;

        return _dbContext.Boards
            .AsNoTracking()
            .Where(board => _dbContext.Users.Any(user =>
                user.Id == userId &&
                user.IsActive &&
                (user.Role == UserRole.Admin ||

                 // Employees retain their existing creator access.
                 (user.Role == UserRole.Employee &&
                  board.OwnerId == userId) ||

                 // Workspace owners can access all boards in their workspace.
                 board.Workspace.OwnerId == userId ||

                 // Explicit board membership.
                 board.Members.Any(member =>
                     member.UserId == userId) ||

                 // Managers need Workspace access.
                 (user.Role == UserRole.Manager &&
                  board.Workspace.Members.Any(member =>
                      member.UserId == userId)))));
    }
    public async Task EnsureCanManageWorkspaceBoardsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(item => item.Id == workspaceId)
            .Select(item => new { item.Id, item.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            throw new NotFoundException("Workspace was not found.");
        }

        var userId = _currentUserService.UserId;
        var userRole = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => (UserRole?)user.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (userRole == UserRole.Admin)
        {
            return;
        }

        var hasWorkspaceAccess = workspace.OwnerId == userId ||
            await _dbContext.WorkspaceMembers.AnyAsync(
                member => member.WorkspaceId == workspaceId &&
                          member.UserId == userId,
                cancellationToken);

        if (userRole != UserRole.Manager || !hasWorkspaceAccess)
        {
            throw new ForbiddenException(
                "Only an active administrator or Manager with workspace access can manage its boards.");
        }
    }

    public async Task<BoardAccessContext> GetAccessAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == boardId,
                cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board was not found.");
        }

        var userId = _currentUserService.UserId;
        var userRole = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => (UserRole?)user.Role)
            .FirstOrDefaultAsync(cancellationToken);
        if (!userRole.HasValue)
        {
            throw new ForbiddenException(
                "Inactive or unknown accounts cannot access boards.");
        }

        var isAdmin = userRole == UserRole.Admin;
        var isManager = userRole == UserRole.Manager;
        var isBoardOwner =
            board.OwnerId == userId &&
            userRole == UserRole.Employee;
        var isWorkspaceOwner = await _dbContext.Workspaces
                    .AnyAsync(
                        workspace => workspace.Id == board.WorkspaceId &&
                                     workspace.OwnerId == userId,
                        cancellationToken);
        var memberRole = await _dbContext.BoardMembers
            .Where(member => member.BoardId == boardId &&
                             member.UserId == userId)
            .Select(member => (BoardMemberRole?)member.Role)
            .FirstOrDefaultAsync(cancellationToken);

        var hasWorkspaceAccess = isWorkspaceOwner ||
            await _dbContext.WorkspaceMembers.AnyAsync(
                member => member.WorkspaceId == board.WorkspaceId &&
                          member.UserId == userId,
                cancellationToken);

        var isWorkspaceManager = isManager && hasWorkspaceAccess;

        var isProjectManager = isManager &&
            hasWorkspaceAccess &&
            board.ProjectId.HasValue &&
            await _dbContext.Projects.AnyAsync(
                project => project.Id == board.ProjectId.Value &&
                           project.WorkspaceId == board.WorkspaceId &&
                           project.ManagerId == userId,
                cancellationToken);

        return new BoardAccessContext
        {
            Board = board,
            IsAdmin = isAdmin,
            IsBoardOwner = isBoardOwner,
            IsWorkspaceOwner = isWorkspaceOwner,
            IsProjectManager = isProjectManager,
            IsWorkspaceManager = isWorkspaceManager,
            MemberRole = memberRole
        };
    }

    public async Task<BoardAccessContext> EnsureCanViewAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(boardId, cancellationToken);
        if (!access.CanView)
        {
            throw new ForbiddenException(
                "You are not authorized to access this board.");
        }

        return access;
    }

    public async Task<BoardAccessContext> EnsureCanEditContentAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(boardId, cancellationToken);
        if (!access.CanEditContent)
        {
            throw new ForbiddenException(
                "You do not have permission to edit content on this board.");
        }

        return access;
    }

    public async Task<BoardAccessContext> EnsureCanManageBoardAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(boardId, cancellationToken);
        if (!access.CanManageBoard)
        {
            throw new ForbiddenException(
                "Only an administrator, board owner, workspace owner, or active Manager with workspace access can manage this board.");
        }

        return access;
    }

    public async Task<BoardAccessContext> EnsureCanCommentAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(boardId, cancellationToken);
        if (!access.CanComment)
        {
            throw new ForbiddenException(
                "You do not have permission to comment on this board.");
        }

        return access;
    }
    public async Task<BoardAccessContext> EnsureCanDeleteBoardAsync(
    Guid boardId,
    CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(boardId, cancellationToken);

        if (!access.CanDeleteBoard)
        {
            throw new ForbiddenException(
                "Only an administrator or workspace owner can delete this board, its columns, or remove board members.");
        }

        return access;
    }

}
