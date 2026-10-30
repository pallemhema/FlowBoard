using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Boards;
using Microsoft.EntityFrameworkCore;
using FlowBoard.Shared.DTOs.Common;
using System.Data;
using SharedBoardPurpose = FlowBoard.Shared.Enums.BoardPurpose;

namespace FlowBoard.Api.Services.Boards;

public class BoardService : IBoardService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBoardAuthorizationService _boardAuthorizationService;

    public BoardService(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService,
        IBoardAuthorizationService boardAuthorizationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _boardAuthorizationService = boardAuthorizationService;
    }

    // ============================================================
    // CREATE
    // ============================================================

    public async Task<BoardResponse> CreateAsync(
        CreateBoardRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateBoardPurposeAsync(
            request.WorkspaceId,
            request.ProjectId,
            request.Purpose,
            cancellationToken);

        var currentUserId = _currentUserService.UserId;

        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            OwnerId = currentUserId,
            WorkspaceId = request.WorkspaceId,
            ProjectId = request.ProjectId,
            Purpose = (BoardPurpose)request.Purpose
        };
        _dbContext.Boards.Add(board);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return MapToResponse(board);
    }

    // ============================================================
    // GET MY BOARDS
    // ============================================================

    public async Task<PagedResponse<BoardResponse>> GetAsync(
        BoardQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _boardAuthorizationService.GetAccessibleBoardsQuery();

        // ============================================================
        // WORKSPACE FILTER
        // ============================================================

        if (request.WorkspaceId.HasValue)
        {
            query = query.Where(x =>
                x.WorkspaceId == request.WorkspaceId.Value);
        }

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

        var boards = await query
            .OrderBy(x => x.Name)
            .Skip(
                (request.Page - 1) *
                request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // ============================================================
        // RESPONSE
        // ============================================================

        return new PagedResponse<BoardResponse>
        {
            Items = boards
                .Select(MapToResponse)
                .ToList(),

            Page = request.Page,

            PageSize = request.PageSize,

            TotalCount = totalCount,

            TotalPages = totalPages
        };
    }

    // ============================================================
    // GET BY ID
    // ============================================================

    public async Task<BoardResponse> GetByIdAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var access = await _boardAuthorizationService.EnsureCanViewAsync(
            boardId,
            cancellationToken);
        return MapToResponse(access.Board);
    }

    // ============================================================
    // UPDATE
    // ============================================================

    public async Task UpdateAsync(
        Guid boardId,
        UpdateBoardRequest request,
        CancellationToken cancellationToken = default)
    {
        var board = await _dbContext.Boards
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException(
                "Board was not found.");
        }

        await _boardAuthorizationService.EnsureCanManageBoardAsync(
            boardId,
            cancellationToken);

        var targetPurpose = request.Purpose.HasValue
            ? request.Purpose.Value
            : (SharedBoardPurpose)board.Purpose;
        if (targetPurpose == SharedBoardPurpose.GeneralLearning &&
            request.ProjectId.HasValue)
        {
            throw new ValidationException(
                "A General/Learning board cannot be associated with a project.");
        }

        var targetProjectId = request.Purpose == SharedBoardPurpose.GeneralLearning
            ? null
            : request.ProjectId ?? board.ProjectId;

        if (targetPurpose != (SharedBoardPurpose)board.Purpose ||
            targetProjectId != board.ProjectId)
        {
            await ValidateBoardPurposeAsync(
                board.WorkspaceId,
                targetProjectId,
                targetPurpose,
                cancellationToken);
        }

        board.Name = request.Name.Trim();
        board.Description = string.IsNullOrWhiteSpace(
            request.Description)
            ? null
            : request.Description.Trim();
        board.ProjectId = targetProjectId;
        board.Purpose = (BoardPurpose)targetPurpose;

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    // ============================================================
    // DELETE
    // ============================================================

    public async Task DeleteAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var board = await _dbContext.Boards
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException(
                "Board was not found.");
        }

        await _boardAuthorizationService.EnsureCanManageBoardAsync(
            boardId,
            cancellationToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (await _dbContext.BoardColumns.AnyAsync(
                column => column.BoardId == boardId,
                cancellationToken) ||
            await _dbContext.BoardMembers.AnyAsync(
                member => member.BoardId == boardId,
                cancellationToken))
        {
            throw new ConflictException(
                "The board cannot be deleted while columns or members are associated with it. Remove them first.");
        }

        _dbContext.Boards.Remove(board);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ValidateBoardPurposeAsync(
        Guid workspaceId,
        Guid? projectId,
        SharedBoardPurpose purpose,
        CancellationToken cancellationToken)
    {
        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == workspaceId,
                cancellationToken);

        if (workspace is null)
        {
            throw new NotFoundException("Workspace was not found.");
        }

        var userId = _currentUserService.UserId;
        var isAdmin = IsCurrentUserAdmin();
        var isWorkspaceOwner = workspace.OwnerId == userId;

        switch (purpose)
        {
            case SharedBoardPurpose.Project:
                if (!projectId.HasValue)
                {
                    throw new ValidationException(
                        "A Project board must be associated with a project.");
                }

                var project = await _dbContext.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item => item.Id == projectId.Value,
                        cancellationToken);

                if (project is null)
                {
                    throw new NotFoundException("Project was not found.");
                }

                if (project.WorkspaceId != workspaceId)
                {
                    throw new ConflictException(
                        "The project must belong to the board's workspace.");
                }

                if (!isAdmin &&
                    (!IsCurrentUserManager() ||
                     project.ManagerId != userId ||
                     (!isWorkspaceOwner &&
                      !await _dbContext.WorkspaceMembers.AnyAsync(
                          member => member.WorkspaceId == workspaceId &&
                                    member.UserId == userId,
                          cancellationToken))))
                {
                    throw new ForbiddenException(
                        "Only an administrator or the responsible Project Manager with workspace access can create or assign a Project board.");
                }

                return;

            case SharedBoardPurpose.GeneralLearning:
                if (projectId.HasValue)
                {
                    throw new ValidationException(
                        "A General/Learning board cannot be associated with a project.");
                }

                if (!isAdmin && !isWorkspaceOwner)
                {
                    throw new ForbiddenException(
                        "Only an administrator or workspace owner can create a General/Learning board.");
                }

                return;

            default:
                throw new ValidationException("The selected board purpose is invalid.");
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

    // ============================================================
    // MAPPING
    // ============================================================

    private static BoardResponse MapToResponse(
        Board board)
    {
        return new BoardResponse
        {
            Id = board.Id,
            Name = board.Name,
            Description = board.Description,
            WorkspaceId = board.WorkspaceId,
            ProjectId = board.ProjectId,
            Purpose = (SharedBoardPurpose)board.Purpose,
            OwnerId = board.OwnerId,
            CreatedAt = board.CreatedAt,
            ModifiedAt = board.ModifiedAt
        };
    }
}