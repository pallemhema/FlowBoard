using FlowBoard.Shared.DTOs.Cards;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Api.Services.Cards;

public interface ICardService
{
    Task<CardResponse> CreateAsync(
        Guid boardColumnId,
        CreateCardRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<CardResponse>> GetByColumnAsync(
        Guid columnId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<CardResponse>> GetByBoardAsync(
        Guid boardId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<CardResponse>> GetByProjectAsync(
        Guid projectId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<CardResponse> GetByIdAsync(
        Guid cardId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid cardId,
        UpdateCardRequest request,
        CancellationToken cancellationToken = default);

    Task MoveAsync(
        Guid cardId,
        MoveCardRequest request,
        CancellationToken cancellationToken = default);

    Task SetAssigneeAsync(
        Guid cardId,
        Guid? employeeId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid cardId,
        CancellationToken cancellationToken = default);
}