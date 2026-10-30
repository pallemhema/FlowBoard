using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Api.Services.TaskAssignments;
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

    public CardService(
        FlowBoardDbContext dbContext,
        IBoardAuthorizationService boardAuthorizationService,
        ITaskAssignmentGuard taskAssignmentGuard)
    {
        _dbContext = dbContext;
        _boardAuthorizationService = boardAuthorizationService;
        _taskAssignmentGuard = taskAssignmentGuard;
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
            .AsNoTracking()
            .Include(x => x.AssignedUser)
            .Where(x => x.BoardColumnId == columnId)
            .AsQueryable();

        // ============================================================
        // SEARCH
        // ============================================================

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(x =>
                x.Title.Contains(search) ||
                (x.Description != null &&
                 x.Description.Contains(search)));
        }

        // ============================================================
        // PRIORITY FILTER
        // ============================================================

        if (request.Priority.HasValue)
        {
            var priority =
                MapToDomainPriority(request.Priority.Value);

            query = query.Where(x =>
                x.Priority == priority);
        }

        // ============================================================
        // ASSIGNED USER FILTER
        // ============================================================

        if (request.AssignedUserId.HasValue)
        {
            query = query.Where(x =>
                x.AssignedUserId ==
                request.AssignedUserId.Value);
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

        var cards = await query
            .OrderBy(x => x.Position)
            .ThenBy(x => x.CreatedAt)
            .Skip(
                (request.Page - 1) *
                request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // ============================================================
        // RESPONSE
        // ============================================================

        return new PagedResponse<CardResponse>
        {
            Items = cards
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