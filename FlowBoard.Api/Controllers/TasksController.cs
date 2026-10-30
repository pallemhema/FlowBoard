using FlowBoard.Api.Authorization;
using FlowBoard.Api.Services.MyTasks;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.MyTasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class TasksController : ControllerBase
{
    private readonly IMyTasksService _myTasksService;

    public TasksController(IMyTasksService myTasksService)
    {
        _myTasksService = myTasksService;
    }

    [HttpGet("my-tasks")]
    public async Task<ActionResult<PagedResponse<MyTaskResponse>>> GetMyTasks(
        [FromQuery] MyTasksQueryRequest request,
        CancellationToken cancellationToken)
    {
        var tasks = await _myTasksService.GetMyTasksAsync(
            request,
            cancellationToken);

        return Ok(tasks);
    }
}
