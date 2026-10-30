using FlowBoard.Shared.DTOs.Cards;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Web.Services.Api;

public interface ICardApiService
{
    Task<PagedResponse<CardResponse>?> GetByColumnAsync(
        Guid columnId,
        CardQueryRequest request);

    Task<CardResponse?> GetByIdAsync(
        Guid cardId);

    Task<CardResponse?> CreateAsync(
        Guid columnId,
        CreateCardRequest request);

    Task<bool> UpdateAsync(
        Guid cardId,
        UpdateCardRequest request);

    Task<bool> MoveAsync(
        Guid cardId,
        MoveCardRequest request);

    Task<bool> DeleteAsync(
        Guid cardId);
}