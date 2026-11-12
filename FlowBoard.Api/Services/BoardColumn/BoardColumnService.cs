using FlowBoard.Api.Exceptions;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Domain.Entities;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Shared.DTOs.BoardColumns;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FlowBoard.Api.Services.BoardColumns;

public class BoardColumnService : IBoardColumnService
{
    private readonly FlowBoardDbContext _dbContext;
    private readonly IBoardAuthorizationService _boardAuthorizationService;

    public BoardColumnService(
        FlowBoardDbContext dbContext,
        IBoardAuthorizationService boardAuthorizationService)
    {
        _dbContext = dbContext;
        _boardAuthorizationService = boardAuthorizationService;
    }

    // ============================================================
    // CREATE COLUMN
    // ============================================================

    public async Task<BoardColumnResponse> CreateAsync(
        Guid boardId,
        CreateBoardColumnRequest request,
        CancellationToken cancellationToken = default)
    {
        await _boardAuthorizationService.EnsureCanEditContentAsync(
            boardId,
            cancellationToken);

        var column = new BoardColumn
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = request.Name.Trim(),
            Position = request.Position
        };

        _dbContext.BoardColumns.Add(column);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return MapToResponse(column);
    }

    // ============================================================
    // GET ALL COLUMNS
    // ============================================================

    public async Task<IReadOnlyList<BoardColumnResponse>> GetAllAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        await _boardAuthorizationService.EnsureCanViewAsync(
            boardId,
            cancellationToken);

        var columns = await _dbContext.BoardColumns
            .AsNoTracking()
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Position)
            .ToListAsync(cancellationToken);

        return columns
            .Select(MapToResponse)
            .ToList();
    }

    // ============================================================
    // UPDATE COLUMN
    // ============================================================

    public async Task<BoardColumnResponse> UpdateAsync(
        Guid boardId,
        Guid columnId,
        UpdateBoardColumnRequest request,
        CancellationToken cancellationToken = default)
    {
        await _boardAuthorizationService.EnsureCanEditContentAsync(
            boardId,
            cancellationToken);

        // Find the column belonging to this board.
        var column = await _dbContext.BoardColumns
            .FirstOrDefaultAsync(
                x => x.Id == columnId &&
                     x.BoardId == boardId,
                cancellationToken);

        if (column is null)
        {
            throw new NotFoundException(
                "Board column was not found.");
        }

        column.Name = request.Name.Trim();
        column.Position = request.Position;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return MapToResponse(column);
    }

    // ============================================================
    // DELETE COLUMN
    // ============================================================

    public async Task DeleteAsync(
        Guid boardId,
        Guid columnId,
        CancellationToken cancellationToken = default)
    {
        await _boardAuthorizationService.EnsureCanDeleteBoardAsync(
            boardId,
            cancellationToken);

        var column = await _dbContext.BoardColumns
            .FirstOrDefaultAsync(
                x => x.Id == columnId &&
                     x.BoardId == boardId,
                cancellationToken);

        if (column is null)
        {
            throw new NotFoundException(
                "Board column was not found.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (await _dbContext.Cards.AnyAsync(
                card => card.BoardColumnId == columnId,
                cancellationToken))
        {
            throw new ConflictException(
                "The column cannot be deleted while cards are associated with it. Remove them first.");
        }

        _dbContext.BoardColumns.Remove(column);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ============================================================
    // MAPPING
    // ============================================================

    private static BoardColumnResponse MapToResponse(
        BoardColumn column)
    {
        return new BoardColumnResponse
        {
            Id = column.Id,
            BoardId = column.BoardId,
            Name = column.Name,
            Position = column.Position
        };
    }
}