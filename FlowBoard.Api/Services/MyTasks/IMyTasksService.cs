using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.MyTasks;

namespace FlowBoard.Api.Services.MyTasks;

public interface IMyTasksService
{
    Task<PagedResponse<MyTaskResponse>> GetMyTasksAsync(
        MyTasksQueryRequest request,
        CancellationToken cancellationToken = default);
}
