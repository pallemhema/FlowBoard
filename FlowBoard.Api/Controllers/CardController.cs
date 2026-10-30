using FlowBoard.Api.Services.Cards;
using FlowBoard.Api.Authorization;
using FlowBoard.Shared.DTOs.Cards;
using FlowBoard.Shared.DTOs.Common;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class CardController : ControllerBase
{
    private readonly ICardService _cardService;

    public CardController(ICardService cardService)
    {
        _cardService = cardService;
    }

    // ============================================================
    // CREATE CARD
    // ============================================================

    [HttpPost]
    public async Task<ActionResult<CardResponse>> Create(
        Guid boardColumnId,
        CreateCardRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _cardService.CreateAsync(
            boardColumnId,
            request,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // GET CARDS BY COLUMN
    // ============================================================

    [HttpGet("column/{columnId:guid}")]
    public async Task<ActionResult<PagedResponse<CardResponse>>> GetByColumn(
       Guid columnId,
       [FromQuery] CardQueryRequest request,
       CancellationToken cancellationToken)
    {
        var result = await _cardService.GetByColumnAsync(
            columnId,
            request,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // GET CARD BY ID
    // ============================================================

    [HttpGet("{cardId:guid}")]
    public async Task<ActionResult<CardResponse>> GetById(
        Guid cardId,
        CancellationToken cancellationToken)
    {
        var result = await _cardService.GetByIdAsync(
            cardId,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // UPDATE CARD
    // ============================================================

    [HttpPut("{cardId:guid}")]
    public async Task<IActionResult> Update(
        Guid cardId,
        UpdateCardRequest request,
        CancellationToken cancellationToken)
    {
        await _cardService.UpdateAsync(
            cardId,
            request,
            cancellationToken);

        return NoContent();
    }

    // ============================================================
    // MOVE CARD
    // ============================================================

    [HttpPatch("{cardId:guid}/move")]
    public async Task<IActionResult> Move(
        Guid cardId,
        MoveCardRequest request,
        CancellationToken cancellationToken)
    {
        await _cardService.MoveAsync(
            cardId,
            request,
            cancellationToken);

        return NoContent();
    }

    // ============================================================
    // DELETE CARD
    // ============================================================

    [HttpDelete("{cardId:guid}")]
    public async Task<IActionResult> Delete(
        Guid cardId,
        CancellationToken cancellationToken)
    {
        await _cardService.DeleteAsync(
            cardId,
            cancellationToken);

        return NoContent();
    }
}