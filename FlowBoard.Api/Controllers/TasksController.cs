using FlowBoard.Api.Authorization;
using FlowBoard.Api.Services.CardComments;
using FlowBoard.Api.Services.Cards;
using FlowBoard.Api.Services.MyTasks;
using FlowBoard.Shared.DTOs.CardComments;
using FlowBoard.Shared.DTOs.Cards;
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
    private readonly ICardService _cardService;
    private readonly ICardCommentService _cardCommentService;

    public TasksController(
        IMyTasksService myTasksService,
        ICardService cardService,
        ICardCommentService cardCommentService)
    {
        _myTasksService = myTasksService;
        _cardService = cardService;
        _cardCommentService = cardCommentService;
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

    [HttpGet("board/{boardId:guid}")]
    public async Task<ActionResult<PagedResponse<CardResponse>>> GetByBoard(
        Guid boardId,
        [FromQuery] CardQueryRequest request,
        CancellationToken cancellationToken)
    {
        var tasks = await _cardService.GetByBoardAsync(
            boardId,
            request,
            cancellationToken);

        return Ok(tasks);
    }

    [HttpGet("project/{projectId:guid}")]
    public async Task<ActionResult<PagedResponse<CardResponse>>> GetByProject(
        Guid projectId,
        [FromQuery] CardQueryRequest request,
        CancellationToken cancellationToken)
    {
        var tasks = await _cardService.GetByProjectAsync(
            projectId,
            request,
            cancellationToken);

        return Ok(tasks);
    }

    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<CardResponse>> GetById(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await _cardService.GetByIdAsync(
            taskId,
            cancellationToken);

        return Ok(task);
    }

    [HttpPost("columns/{columnId:guid}/cards")]
    public async Task<ActionResult<CardResponse>> Create(
        Guid columnId,
        CreateCardRequest request,
        CancellationToken cancellationToken)
    {
        var task = await _cardService.CreateAsync(
            columnId,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { taskId = task.Id },
            task);
    }

    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> Update(
        Guid taskId,
        UpdateCardRequest request,
        CancellationToken cancellationToken)
    {
        await _cardService.UpdateAsync(taskId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{taskId:guid}/assignee")]
    public async Task<IActionResult> Assign(
        Guid taskId,
        AssignTaskRequest request,
        CancellationToken cancellationToken)
    {
        await _cardService.SetAssigneeAsync(
            taskId,
            request.EmployeeId,
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{taskId:guid}/assignee")]
    public async Task<IActionResult> Unassign(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        await _cardService.SetAssigneeAsync(
            taskId,
            null,
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        await _cardService.DeleteAsync(taskId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{taskId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CardCommentResponse>>> GetComments(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var comments = await _cardCommentService.GetByCardAsync(
            taskId,
            cancellationToken);

        return Ok(comments);
    }
}
