using FlowBoard.Shared.DTOs.CardComments;
using FlowBoard.Shared.DTOs.Cards;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.MyTasks;

namespace FlowBoard.Web.Services.Api;

public interface ITaskApiService
{
    Task<PagedResponse<MyTaskResponse>?> GetMyTasksAsync(
        MyTasksQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<CardResponse>?> GetByBoardAsync(
        Guid boardId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<CardResponse>?> GetByProjectAsync(
        Guid projectId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<CardResponse?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<CardResponse?> CreateAsync(
        Guid columnId,
        CreateCardRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        Guid taskId,
        UpdateCardRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> AssignAsync(
        Guid taskId,
        AssignTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UnassignAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CardCommentResponse>?> GetCommentsAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);
}
