using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Domain.Entities;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.CardComments;
using Microsoft.EntityFrameworkCore;

namespace FlowBoard.Api.Services.CardComments;

public class CardCommentService : ICardCommentService
{
    private static readonly TimeSpan CommentEditWindow =
        TimeSpan.FromMinutes(3);

    private readonly FlowBoardDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBoardAuthorizationService _boardAuthorizationService;

    public CardCommentService(
        FlowBoardDbContext dbContext,
        ICurrentUserService currentUserService,
        IBoardAuthorizationService boardAuthorizationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _boardAuthorizationService = boardAuthorizationService;
    }

    // ============================================================
    // ADD COMMENT
    // ============================================================

    public async Task<CardCommentResponse> AddAsync(
        Guid cardId,
        CreateCardCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;

        var card = await _dbContext.Cards
            .Include(x => x.BoardColumn)
            .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(
                x => x.Id == cardId,
                cancellationToken);

        if (card is null)
        {
            throw new NotFoundException(
                "Card was not found.");
        }

        await _boardAuthorizationService.EnsureCanCommentAsync(
            card.BoardColumn.BoardId,
            cancellationToken);

        var comment = new CardComment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = currentUserId,
            Content = request.Content.Trim()
        };

        _dbContext.CardComments.Add(comment);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetCommentResponseAsync(
            comment.Id,
            cancellationToken);
    }

    // ============================================================
    // GET COMMENTS
    // ============================================================

    public async Task<IReadOnlyList<CardCommentResponse>> GetByCardAsync(
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;

        var card = await _dbContext.Cards
            .Include(x => x.BoardColumn)
            .ThenInclude(x => x.Board)
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

        var comments = await _dbContext.CardComments
            .AsNoTracking()
            .Where(x => x.CardId == cardId)
            .Include(x => x.User)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return comments
            .Select(MapToResponse)
            .ToList();
    }

    // ============================================================
    // UPDATE COMMENT
    // ============================================================

    public async Task UpdateAsync(
        Guid commentId,
        UpdateCardCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;

        var comment = await _dbContext.CardComments
            .Include(item => item.Card)
            .ThenInclude(item => item.BoardColumn)
            .FirstOrDefaultAsync(
                x => x.Id == commentId,
                cancellationToken);

        if (comment is null)
        {
            throw new NotFoundException(
                "Comment was not found.");
        }

        await _boardAuthorizationService.EnsureCanViewAsync(
            comment.Card.BoardColumn.BoardId,
            cancellationToken);

        // Only the author can edit the comment.
        if (comment.UserId != currentUserId)
        {
            throw new ForbiddenException(
                "You can only edit your own comments.");
        }

        // Comments can only be edited for 3 minutes.
        var now = DateTime.UtcNow;
        var elapsed = now - comment.CreatedAt;

        if (elapsed < TimeSpan.Zero || elapsed >= CommentEditWindow)
        {
            throw new ConflictException(
                "Comments can only be edited within 3 minutes of creation.");
        }

        comment.Content = request.Content.Trim();

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    // ============================================================
    // DELETE COMMENT
    // ============================================================

    public async Task DeleteAsync(
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;

        var comment = await _dbContext.CardComments
            .Include(x => x.Card)
            .ThenInclude(x => x.BoardColumn)
            .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(
                x => x.Id == commentId,
                cancellationToken);

        if (comment is null)
        {
            throw new NotFoundException(
                "Comment was not found.");
        }

        var board = comment.Card.BoardColumn.Board;
        await _boardAuthorizationService.EnsureCanViewAsync(
            board.Id,
            cancellationToken);

        if (comment.UserId != currentUserId)
        {
            throw new ForbiddenException(
                "You can only delete your own comments.");
        }

        _dbContext.CardComments.Remove(comment);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    // ============================================================
    // GET SINGLE COMMENT
    // ============================================================

    private async Task<CardCommentResponse> GetCommentResponseAsync(
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var comment = await _dbContext.CardComments
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.Id == commentId,
                cancellationToken);

        if (comment is null)
        {
            throw new NotFoundException(
                "Comment was not found.");
        }

        return MapToResponse(comment);
    }

    // ============================================================
    // ENTITY -> RESPONSE DTO
    // ============================================================

    private static CardCommentResponse MapToResponse(
        CardComment comment)
    {
        return new CardCommentResponse
        {
            Id = comment.Id,
            CardId = comment.CardId,
            UserId = comment.UserId,
            Content = comment.Content,
            UserName =
                $"{comment.User.FirstName} {comment.User.LastName}",
            CreatedAt = comment.CreatedAt,
            ModifiedAt = comment.ModifiedAt
        };
    }
}