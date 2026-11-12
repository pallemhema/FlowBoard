using FlowBoard.Api.Services.Boards;
using FlowBoard.Api.Authorization;
using FlowBoard.Shared.DTOs.Boards;
using FlowBoard.Shared.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
[Route("api/boards")]
public class BoardsController : ControllerBase
{
    private readonly IBoardService _boardService;

    public BoardsController(
        IBoardService boardService)
    {
        _boardService = boardService;
    }

    // ============================================================
    // CREATE
    // ============================================================

    [HttpPost]
    public async Task<ActionResult<BoardResponse>> Create(
        CreateBoardRequest request,
        CancellationToken cancellationToken)
    {
        var board = await _boardService.CreateAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { boardId = board.Id },
            board);
    }

    // ============================================================
    // GET MY BOARDS
    // ============================================================

    [HttpGet]
    public async Task<ActionResult<PagedResponse<BoardResponse>>> Get(
     [FromQuery] BoardQueryRequest request,
     CancellationToken cancellationToken)
    {
        var result = await _boardService.GetAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("workspaces/{workspaceId:guid}/projects")]
    public async Task<ActionResult<IReadOnlyList<BoardProjectOptionResponse>>> GetWorkspaceProjects(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var projects = await _boardService.GetWorkspaceProjectsAsync(
            workspaceId,
            cancellationToken);

        return Ok(projects);
    }

    // ============================================================
    // GET BY ID
    // ============================================================

    [HttpGet("{boardId:guid}")]
    public async Task<ActionResult<BoardResponse>> GetById(
        Guid boardId,
        CancellationToken cancellationToken)
    {
        var board = await _boardService.GetByIdAsync(
            boardId,
            cancellationToken);

        return Ok(board);
    }

    // ============================================================
    // UPDATE
    // ============================================================

    [HttpPut("{boardId:guid}")]
    public async Task<IActionResult> Update(
        Guid boardId,
        UpdateBoardRequest request,
        CancellationToken cancellationToken)
    {
        await _boardService.UpdateAsync(
            boardId,
            request,
            cancellationToken);

        return NoContent();
    }

    // ============================================================
    // DELETE
    // ============================================================

    [HttpDelete("{boardId:guid}")]
    public async Task<IActionResult> Delete(
        Guid boardId,
        CancellationToken cancellationToken)
    {
        await _boardService.DeleteAsync(
            boardId,
            cancellationToken);

        return NoContent();
    }
}