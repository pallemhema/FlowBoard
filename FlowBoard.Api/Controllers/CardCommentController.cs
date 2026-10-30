using FlowBoard.Api.Services.CardComments;
using FlowBoard.Api.Authorization;
using FlowBoard.Shared.DTOs.CardComments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class CardCommentController : ControllerBase
{
    private readonly ICardCommentService _cardCommentService;

    public CardCommentController(
        ICardCommentService cardCommentService)
    {
        _cardCommentService = cardCommentService;
    }

    // ============================================================
    // ADD COMMENT
    // ============================================================

    [HttpPost("card/{cardId:guid}")]
    public async Task<ActionResult<CardCommentResponse>> Add(
        Guid cardId,
        CreateCardCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _cardCommentService.AddAsync(
            cardId,
            request,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // GET COMMENTS
    // ============================================================

    [HttpGet("card/{cardId:guid}")]
    public async Task<ActionResult<IReadOnlyList<CardCommentResponse>>> GetByCard(
        Guid cardId,
        CancellationToken cancellationToken)
    {
        var result = await _cardCommentService.GetByCardAsync(
            cardId,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // UPDATE COMMENT
    // ============================================================

    [HttpPut("{commentId:guid}")]
    public async Task<IActionResult> Update(
        Guid commentId,
        UpdateCardCommentRequest request,
        CancellationToken cancellationToken)
    {
        await _cardCommentService.UpdateAsync(
            commentId,
            request,
            cancellationToken);

        return NoContent();
    }

    // ============================================================
    // DELETE COMMENT
    // ============================================================

    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> Delete(
        Guid commentId,
        CancellationToken cancellationToken)
    {
        await _cardCommentService.DeleteAsync(
            commentId,
            cancellationToken);

        return NoContent();
    }
}