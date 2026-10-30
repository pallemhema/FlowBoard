using FlowBoard.Api.Services.BoardColumns;
using FlowBoard.Api.Authorization;
using FlowBoard.Shared.DTOs.BoardColumns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Route("api/boards/{boardId:guid}/columns")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class BoardColumnsController : ControllerBase
{
    private readonly IBoardColumnService _boardColumnService;

    public BoardColumnsController(
        IBoardColumnService boardColumnService)
    {
        _boardColumnService = boardColumnService;
    }

    // ============================================================
    // CREATE
    // ============================================================

    [HttpPost]
    public async Task<ActionResult<BoardColumnResponse>> Create(
        Guid boardId,
        CreateBoardColumnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _boardColumnService.CreateAsync(
            boardId,
            request,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // GET ALL
    // ============================================================

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BoardColumnResponse>>> GetAll(
        Guid boardId,
        CancellationToken cancellationToken)
    {
        var result = await _boardColumnService.GetAllAsync(
            boardId,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // UPDATE
    // ============================================================

    [HttpPut("{columnId:guid}")]
    public async Task<ActionResult<BoardColumnResponse>> Update(
        Guid boardId,
        Guid columnId,
        UpdateBoardColumnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _boardColumnService.UpdateAsync(
            boardId,
            columnId,
            request,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // DELETE
    // ============================================================

    [HttpDelete("{columnId:guid}")]
    public async Task<IActionResult> Delete(
        Guid boardId,
        Guid columnId,
        CancellationToken cancellationToken)
    {
        await _boardColumnService.DeleteAsync(
            boardId,
            columnId,
            cancellationToken);

        return NoContent();
    }
}