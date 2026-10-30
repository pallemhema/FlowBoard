using FlowBoard.Shared.DTOs.CardComments;

namespace FlowBoard.Api.Services.CardComments;

public interface ICardCommentService
{
    Task<CardCommentResponse> AddAsync(
        Guid cardId,
        CreateCardCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CardCommentResponse>> GetByCardAsync(
        Guid cardId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid commentId,
        UpdateCardCommentRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid commentId,
        CancellationToken cancellationToken = default);
}