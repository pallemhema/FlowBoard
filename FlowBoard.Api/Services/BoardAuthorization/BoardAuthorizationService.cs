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
        if (IsCurrentUserAdmin())
        {
            return _dbContext.Boards.AsNoTracking();
        }

        var isManager = IsCurrentUserManager();
        return _dbContext.Boards
            .AsNoTracking()
            .Where(board =>
                board.OwnerId == userId ||
                board.Workspace.OwnerId == userId ||
                board.Members.Any(member => member.UserId == userId) ||
                (isManager &&
                 board.ProjectId.HasValue &&
                 board.Project != null &&
                 board.Project.WorkspaceId == board.WorkspaceId &&
                 board.Project.ManagerId == userId &&
                 (board.Workspace.OwnerId == userId ||
                  board.Workspace.Members.Any(
                      member => member.UserId == userId))));
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
        var isAdmin = IsCurrentUserAdmin();
        var isBoardOwner = board.OwnerId == userId;
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

        var isProjectManager = IsCurrentUserManager() &&
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
                "Only an administrator, board owner, workspace owner, or responsible Project Manager can manage this board.");
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
}
