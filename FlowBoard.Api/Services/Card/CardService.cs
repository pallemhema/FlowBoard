using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Api.Services.TaskAssignments;
using FlowBoard.Api.Services.Projects;
using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.Cards;
using Microsoft.EntityFrameworkCore;
using FlowBoard.Shared.DTOs.Common;
using System.Data;

namespace FlowBoard.Api.Services.Cards;

public class CardService : ICardService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly IBoardAuthorizationService _boardAuthorizationService;
    private readonly ITaskAssignmentGuard _taskAssignmentGuard;
    private readonly IProjectService _projectService;

    public CardService(
        FlowBoardDbContext dbContext,
        IBoardAuthorizationService boardAuthorizationService,
        ITaskAssignmentGuard taskAssignmentGuard,
        IProjectService projectService)
    {
        _dbContext = dbContext;
        _boardAuthorizationService = boardAuthorizationService;
        _taskAssignmentGuard = taskAssignmentGuard;
        _projectService = projectService;
    }

    // ============================================================
    // CREATE
    // ============================================================

    public async Task<CardResponse> CreateAsync(
        Guid boardColumnId,
        CreateCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var column = await _dbContext.BoardColumns
            .Include(x => x.Board)
            .FirstOrDefaultAsync(
                x => x.Id == boardColumnId,
                cancellationToken);

        if (column is null)
        {
            throw new NotFoundException(
                "Board column was not found.");
        }

        await _boardAuthorizationService.EnsureCanEditContentAsync(
            column.BoardId,
            cancellationToken);

        var card = new Card
        {
            Id = Guid.NewGuid(),
            BoardColumnId = boardColumnId,
            AssignedUserId = request.AssignedUserId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = MapToDomainPriority(request.Priority),
            DueDate = request.DueDate,
            Position = request.Position
        };

        if (request.AssignedUserId.HasValue)
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            await _taskAssignmentGuard.ValidateAssigneeAsync(
                column.BoardId,
                request.AssignedUserId.Value,
                cancellationToken);

            _dbContext.Cards.Add(card);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            _dbContext.Cards.Add(card);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(
            card.Id,
            cancellationToken);
    }

    // ============================================================
    // GET BY COLUMN
    // ============================================================
    public async Task<PagedResponse<CardResponse>> GetByColumnAsync(
        Guid columnId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        // ============================================================
        // GET COLUMN + BOARD
        // ============================================================

        var column = await _dbContext.BoardColumns
            .AsNoTracking()
            .Include(x => x.Board)
            .FirstOrDefaultAsync(
                x => x.Id == columnId,
                cancellationToken);

        if (column is null)
        {
            throw new NotFoundException(
                "Board column was not found.");
        }

        // ============================================================
        // AUTHORIZATION
        // ============================================================

        await _boardAuthorizationService.EnsureCanViewAsync(
            column.BoardId,
            cancellationToken);

        // ============================================================
        // BUILD QUERY
        // ============================================================

        var query = _dbContext.Cards
            .Where(card => card.BoardColumnId == columnId);

        return await GetPagedCardsAsync(query, request, cancellationToken);
    }

    public async Task<PagedResponse<CardResponse>> GetByBoardAsync(
        Guid boardId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var boardExists = await _dbContext.Boards
            .AnyAsync(board => board.Id == boardId, cancellationToken);

        if (!boardExists)
        {
            throw new NotFoundException("Board was not found.");
        }

        await _boardAuthorizationService.EnsureCanViewAsync(
            boardId,
            cancellationToken);

        var query = _dbContext.Cards
            .Where(card => card.BoardColumn.BoardId == boardId);

        return await GetPagedCardsAsync(query, request, cancellationToken);
    }

    public async Task<PagedResponse<CardResponse>> GetByProjectAsync(
        Guid projectId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        await _projectService.GetByIdAsync(projectId, cancellationToken);

        var accessibleBoardIds = _boardAuthorizationService
            .GetAccessibleBoardsQuery()
            .Where(board => board.ProjectId == projectId)
            .Select(board => board.Id);
        var query = _dbContext.Cards
            .Where(card =>
                card.BoardColumn.Board.ProjectId == projectId &&
                accessibleBoardIds.Contains(card.BoardColumn.BoardId));

        return await GetPagedCardsAsync(query, request, cancellationToken);
    }
    // ============================================================
    // GET BY ID
    // ============================================================

    public async Task<CardResponse> GetByIdAsync(
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.AssignedUser)
            .Include(x => x.BoardColumn)
            .FirstOrDefaultAsync(
                x => x.Id == cardId,
                cancellationToken);

        if (card is null)
        {
            throw new NotFoundException(
                "Card was not found.");
        }

        await _boardAuthorizationService.EnsureCanViewAsync(
            card.BoardColumn.BoardId,
            cancellationToken);

        return MapToResponse(card);
    }

    // ============================================================
    // UPDATE
    // ============================================================

    public async Task UpdateAsync(
        Guid cardId,
        UpdateCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(x => x.BoardColumn)
            .FirstOrDefaultAsync(
                x => x.Id == cardId,
                cancellationToken);

        if (card is null)
        {
            throw new NotFoundException(
                "Card was not found.");
        }

        await _boardAuthorizationService.EnsureCanEditContentAsync(
            card.BoardColumn.BoardId,
            cancellationToken);

        card.Title = request.Title.Trim();
        card.Description = request.Description?.Trim();
        card.Priority = MapToDomainPriority(request.Priority);
        card.DueDate = request.DueDate;

        if (request.AssignedUserId.HasValue)
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            await _taskAssignmentGuard.ValidateAssigneeAsync(
                card.BoardColumn.BoardId,
                request.AssignedUserId.Value,
                cancellationToken);

            if (card.AssignedUserId != request.AssignedUserId)
            {
                card.AssignedUserId = request.AssignedUserId;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            card.AssignedUserId = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // ============================================================
    // MOVE
    // ============================================================

    public async Task MoveAsync(
        Guid cardId,
        MoveCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(x => x.BoardColumn)
            .FirstOrDefaultAsync(
                x => x.Id == cardId,
                cancellationToken);

        if (card is null)
        {
            throw new NotFoundException(
                "Card was not found.");
        }

        await _boardAuthorizationService.EnsureCanEditContentAsync(
            card.BoardColumn.BoardId,
            cancellationToken);

        var targetColumn = await _dbContext.BoardColumns
            .FirstOrDefaultAsync(
                x => x.Id == request.BoardColumnId,
                cancellationToken);

        if (targetColumn is null)
        {
            throw new NotFoundException(
                "Target board column was not found.");
        }

        // Make sure the target column belongs
        // to the same board as the current card.
        if (targetColumn.BoardId != card.BoardColumn.BoardId)
        {
            throw new ConflictException(
                "A card cannot be moved to a column from another board.");
        }

        card.BoardColumnId = request.BoardColumnId;
        card.Position = request.Position;

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task SetAssigneeAsync(
        Guid cardId,
        Guid? employeeId,
        CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(item => item.BoardColumn)
            .FirstOrDefaultAsync(item => item.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card was not found.");
        }

        await _boardAuthorizationService.EnsureCanEditContentAsync(
            card.BoardColumn.BoardId,
            cancellationToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (employeeId.HasValue)
        {
            await _taskAssignmentGuard.ValidateAssigneeAsync(
                card.BoardColumn.BoardId,
                employeeId.Value,
                cancellationToken);
        }

        card.AssignedUserId = employeeId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ============================================================
    // DELETE
    // ============================================================

    public async Task DeleteAsync(
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(x => x.BoardColumn)
            .FirstOrDefaultAsync(
                x => x.Id == cardId,
                cancellationToken);

        if (card is null)
        {
            throw new NotFoundException(
                "Card was not found.");
        }

        await _boardAuthorizationService.EnsureCanEditContentAsync(
            card.BoardColumn.BoardId,
            cancellationToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (await _dbContext.CardComments.AnyAsync(
                comment => comment.CardId == cardId,
                cancellationToken))
        {
            throw new ConflictException(
                "The card cannot be deleted while comments are associated with it. Remove them first.");
        }

        _dbContext.Cards.Remove(card);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ============================================================
    // ASSIGNED USER VALIDATION
    // ============================================================

    // ============================================================
    // MAPPING
    // ============================================================

    private static CardResponse MapToResponse(Card card)
    {
        return new CardResponse
        {
            Id = card.Id,
            BoardColumnId = card.BoardColumnId,
            AssignedUserId = card.AssignedUserId,
            Title = card.Title,
            Description = card.Description,
            Priority = MapToSharedPriority(card.Priority),
            DueDate = card.DueDate,
            Position = card.Position,
            AssignedUserName = card.AssignedUser is null
                ? null
                : $"{card.AssignedUser.FirstName} {card.AssignedUser.LastName}"
        };
    }

    private async Task<PagedResponse<CardResponse>> GetPagedCardsAsync(
        IQueryable<Card> query,
        CardQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
        {
            throw new ValidationException(
                "Page must be greater than zero and page size must be between 1 and 100.");
        }

        if (request.Priority.HasValue &&
            !Enum.IsDefined(request.Priority.Value))
        {
            throw new ValidationException("The selected card priority is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(card =>
                card.Title.Contains(search) ||
                (card.Description != null &&
                 card.Description.Contains(search)));
        }

        if (request.Priority.HasValue)
        {
            var priority = MapToDomainPriority(request.Priority.Value);
            query = query.Where(card => card.Priority == priority);
        }

        if (request.AssignedUserId.HasValue)
        {
            query = query.Where(
                card => card.AssignedUserId == request.AssignedUserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var cards = await query
            .AsNoTracking()
            .Include(card => card.AssignedUser)
            .OrderBy(card => card.Position)
            .ThenBy(card => card.CreatedAt)
            .ThenBy(card => card.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<CardResponse>
        {
            Items = cards.Select(MapToResponse).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)request.PageSize)
        };
    }

    private static CardPriority MapToDomainPriority(
        Shared.Enums.CardPriority priority)
    {
        return priority switch
        {
            Shared.Enums.CardPriority.Low =>
                CardPriority.Low,

            Shared.Enums.CardPriority.Medium =>
                CardPriority.Medium,

            Shared.Enums.CardPriority.High =>
                CardPriority.High,

            _ => throw new ArgumentException(
                "Invalid card priority.")
        };
    }

    private static Shared.Enums.CardPriority MapToSharedPriority(
        CardPriority priority)
    {
        return priority switch
        {
            CardPriority.Low =>
                Shared.Enums.CardPriority.Low,

            CardPriority.Medium =>
                Shared.Enums.CardPriority.Medium,

            CardPriority.High =>
                Shared.Enums.CardPriority.High,

            _ => throw new ArgumentException(
                "Invalid card priority.")
        };
    }

}