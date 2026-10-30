using FlowBoard.Api.Services.BoardMembers;
using FlowBoard.Api.Authorization;
using FlowBoard.Shared.DTOs.BoardMembers;
using FlowBoard.Shared.DTOs.Common;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
[Route("api/boards/{boardId:guid}/members")]

public class BoardMembersController : ControllerBase
{
    private readonly IBoardMemberService _boardMemberService;

    public BoardMembersController(
        IBoardMemberService boardMemberService)
    {
        _boardMemberService = boardMemberService;
    }

    // ============================================================
    // GET MEMBERS
    // ============================================================

    [HttpGet]
    public async Task<ActionResult<PagedResponse<BoardMemberResponse>>> GetMembers(
        Guid boardId,
        [FromQuery] PagedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _boardMemberService.GetMembersAsync(
            boardId,
            request,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // ADD MEMBER
    // ============================================================

    [HttpPost]
    public async Task<ActionResult<BoardMemberResponse>> AddMember(
        Guid boardId,
        AddBoardMemberRequest request,
        CancellationToken cancellationToken)
    {
        var member = await _boardMemberService.AddAsync(
            boardId,
            request,
            cancellationToken);

        return Ok(member);
    }

    // ============================================================
    // UPDATE MEMBER ROLE
    // ============================================================

    [HttpPut("{userId:guid}/role")]
    public async Task<IActionResult> UpdateRole(
        Guid boardId,
        Guid userId,
        UpdateBoardMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        await _boardMemberService.UpdateRoleAsync(
            boardId,
            userId,
            request,
            cancellationToken);

        return NoContent();
    }

    // ============================================================
    // REMOVE MEMBER
    // ============================================================

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid boardId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _boardMemberService.RemoveAsync(
            boardId,
            userId,
            cancellationToken);

        return NoContent();
    }
}