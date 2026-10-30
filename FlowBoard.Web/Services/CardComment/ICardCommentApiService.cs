using FlowBoard.Shared.DTOs.CardComments;

namespace FlowBoard.Web.Services.Api;

public interface ICardCommentApiService
{
    Task<IReadOnlyList<CardCommentResponse>?> GetByCardAsync(
        Guid cardId);

    Task<CardCommentResponse?> AddAsync(
        Guid cardId,
        CreateCardCommentRequest request);

    Task<bool> UpdateAsync(
        Guid commentId,
        UpdateCardCommentRequest request);

    Task<bool> DeleteAsync(
        Guid commentId);
}