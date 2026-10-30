using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Api.Services.TaskAssignments;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.BoardMembers;
using Microsoft.EntityFrameworkCore;
using FlowBoard.Shared.DTOs.Common;
using System.Data;

namespace FlowBoard.Api.Services.BoardMembers;

public class BoardMemberService : IBoardMemberService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBoardAuthorizationService _boardAuthorizationService;
    private readonly ITaskAssignmentGuard _taskAssignmentGuard;

    public BoardMemberService(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService,
        IBoardAuthorizationService boardAuthorizationService,
        ITaskAssignmentGuard taskAssignmentGuard)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _boardAuthorizationService = boardAuthorizationService;
        _taskAssignmentGuard = taskAssignmentGuard;
    }

    // ============================================================
    // ADD MEMBER
    // ============================================================

    public async Task<BoardMemberResponse> AddAsync(
        Guid boardId,
        AddBoardMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await _boardAuthorizationService.EnsureCanManageBoardAsync(
            boardId,
            cancellationToken);
        var board = access.Board;

        if (!Enum.IsDefined(request.Role))
        {
            throw new ValidationException("The selected board member role is invalid.");
        }

        if (request.Role == FlowBoard.Shared.Enums.BoardMemberRole.Owner)
        {
            throw new ConflictException(
                "The Owner role cannot be assigned to a board member.");
        }

        if (request.UserId == board.OwnerId)
        {
            throw new ConflictException(
                "The board owner cannot be added as a member.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == request.UserId,
                cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User was not found.");
        }

        if (user.Role != UserRole.Employee)
        {
            throw new ValidationException(
                "Only Employees can be added as board members.");
        }

        var isEligibleForBoard = board.Purpose switch
        {
            Domain.Enums.BoardPurpose.Project =>
                board.ProjectId.HasValue &&
                await _dbContext.ProjectMembers.AnyAsync(
                    member => member.ProjectId == board.ProjectId.Value &&
                              member.UserId == user.Id,
                    cancellationToken),
            Domain.Enums.BoardPurpose.GeneralLearning =>
                await _dbContext.WorkspaceMembers.AnyAsync(
                    member => member.WorkspaceId == board.WorkspaceId &&
                              member.UserId == user.Id,
                    cancellationToken),
            _ => false
        };

        if (!isEligibleForBoard)
        {
            throw new ConflictException(
                board.Purpose == Domain.Enums.BoardPurpose.Project
                    ? "The employee must be a member of the associated project."
                    : "The employee must be a member of the board's workspace.");
        }

        if (string.Equals(
                _currentUserService.Role,
                nameof(UserRole.Manager),
                StringComparison.Ordinal) &&
            !await _dbContext.ManagerEmployees.AnyAsync(
                relationship => relationship.ManagerId == _currentUserService.UserId &&
                                relationship.EmployeeId == user.Id,
                cancellationToken))
        {
            throw new ForbiddenException(
                "Managers may add only employees assigned to them.");
        }

        if (await _dbContext.BoardMembers.AnyAsync(
                member => member.BoardId == boardId &&
                          member.UserId == user.Id,
                cancellationToken))
        {
            throw new ConflictException(
                "User is already a member of this board.");
        }

        var member = new BoardMember
        {
            BoardId = boardId,
            UserId = request.UserId,
            Role = MapToDomainRole(request.Role),
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.BoardMembers.Add(member);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(member).State = EntityState.Detached;
            if (await _dbContext.BoardMembers.AnyAsync(
                    existing => existing.BoardId == boardId &&
                                existing.UserId == user.Id,
                    cancellationToken))
            {
                throw new ConflictException(
                    "User is already a member of this board.");
            }

            throw;
        }

        return await GetMemberResponseAsync(
            member.BoardId,
            member.UserId,
            cancellationToken);
    }

    // ============================================================
    // GET MEMBERS
    // ============================================================

    public async Task<PagedResponse<BoardMemberResponse>> GetMembersAsync(
    Guid boardId,
    PagedSearchRequest request,
    CancellationToken cancellationToken = default)
    {
        await _boardAuthorizationService.EnsureCanViewAsync(
            boardId,
            cancellationToken);

        // ============================================================
        // BUILD QUERY
        // ============================================================

        var query = _dbContext.BoardMembers
            .AsNoTracking()
            .Where(x => x.BoardId == boardId)
            .Include(x => x.User)
            .AsQueryable();

        // ============================================================
        // SEARCH
        // ============================================================

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(x =>
                x.User.Email.Contains(search) ||
                x.User.FirstName.Contains(search) ||
                x.User.LastName.Contains(search));
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

        var members = await query
            .OrderBy(x => x.JoinedAt)
            .Skip(
                (request.Page - 1) *
                request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // ============================================================
        // RESPONSE
        // ============================================================

        return new PagedResponse<BoardMemberResponse>
        {
            Items = members
                .Select(MapToResponse)
                .ToList(),

            Page = request.Page,

            PageSize = request.PageSize,

            TotalCount = totalCount,

            TotalPages = totalPages
        };
    }

    // ============================================================
    // UPDATE MEMBER ROLE
    // ============================================================

    public async Task UpdateRoleAsync(
        Guid boardId,
        Guid userId,
        UpdateBoardMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await _boardAuthorizationService.EnsureCanManageBoardAsync(
            boardId,
            cancellationToken);

        if (userId == access.Board.OwnerId)
        {
            throw new ConflictException(
                "The board owner cannot have a board member role.");
        }

        if (!Enum.IsDefined(request.Role))
        {
            throw new ValidationException("The selected board member role is invalid.");
        }

        var member = await _dbContext.BoardMembers
            .FirstOrDefaultAsync(
                x => x.BoardId == boardId &&
                     x.UserId == userId,
                cancellationToken);

        if (member is null)
        {
            throw new NotFoundException(
                "Board member was not found.");
        }

        // A member cannot be given the Owner role.
        // Ownership is represented by Board.OwnerId.
        if (request.Role == FlowBoard.Shared.Enums.BoardMemberRole.Owner)
        {
            throw new ConflictException(
                "The Owner role cannot be assigned to a board member.");
        }

        member.Role = MapToDomainRole(request.Role);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    // ============================================================
    // REMOVE MEMBER
    // ============================================================

    public async Task RemoveAsync(
        Guid boardId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var access = await _boardAuthorizationService.EnsureCanManageBoardAsync(
            boardId,
            cancellationToken);

        if (userId == access.Board.OwnerId)
        {
            throw new ConflictException(
                "The board owner cannot be removed through board membership.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var member = await _dbContext.BoardMembers
            .FirstOrDefaultAsync(
                x => x.BoardId == boardId &&
                     x.UserId == userId,
                cancellationToken);

        if (member is null)
        {
            throw new NotFoundException(
                "Board member was not found.");
        }

        await _taskAssignmentGuard.EnsureBoardMemberCanBeRemovedAsync(
            boardId,
            userId,
            cancellationToken);

        _dbContext.BoardMembers.Remove(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ============================================================
    // GET SINGLE MEMBER
    // ============================================================

    private async Task<BoardMemberResponse> GetMemberResponseAsync(
        Guid boardId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var member = await _dbContext.BoardMembers
            .AsNoTracking()
            .Include(x => x.User)
            .FirstAsync(
                x => x.BoardId == boardId &&
                     x.UserId == userId,
                cancellationToken);

        return MapToResponse(member);
    }

    // ============================================================
    // DOMAIN -> SHARED DTO
    // ============================================================

    private static BoardMemberResponse MapToResponse(
        BoardMember member)
    {
        return new BoardMemberResponse
        {
            UserId = member.UserId,
            Email = member.User.Email,
            FirstName = member.User.FirstName,
            LastName = member.User.LastName,
            Role = MapToSharedRole(member.Role),
            JoinedAt = member.JoinedAt
        };
    }

    // ============================================================
    // SHARED -> DOMAIN
    // ============================================================

    private static Domain.Enums.BoardMemberRole MapToDomainRole(
        Shared.Enums.BoardMemberRole role)
    {
        return role switch
        {
            Shared.Enums.BoardMemberRole.Viewer =>
                Domain.Enums.BoardMemberRole.Viewer,

            Shared.Enums.BoardMemberRole.Editor =>
                Domain.Enums.BoardMemberRole.Editor,

            Shared.Enums.BoardMemberRole.Owner =>
                Domain.Enums.BoardMemberRole.Owner,

            _ => throw new ValidationException(
                "Invalid board member role.")
        };
    }

    // ============================================================
    // DOMAIN -> SHARED
    // ============================================================

    private static Shared.Enums.BoardMemberRole MapToSharedRole(
        Domain.Enums.BoardMemberRole role)
    {
        return role switch
        {
            Domain.Enums.BoardMemberRole.Viewer =>
                Shared.Enums.BoardMemberRole.Viewer,

            Domain.Enums.BoardMemberRole.Editor =>
                Shared.Enums.BoardMemberRole.Editor,

            Domain.Enums.BoardMemberRole.Owner =>
                Shared.Enums.BoardMemberRole.Owner,

            _ => throw new ArgumentException(
                "Invalid board member role.")
        };
    }
}