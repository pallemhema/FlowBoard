using FlowBoard.Api.Authorization;
using FlowBoard.Api.Services.Teams;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Route("api/teams")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamsController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<TeamResponse>> Create(
        CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var team = await _teamService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { teamId = team.TeamId },
            team);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<PagedResponse<TeamResponse>>> GetAll(
        [FromQuery] PagedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var teams = await _teamService.GetAllAsync(request, cancellationToken);
        return Ok(teams);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResponse<TeamResponse>>> GetMine(
        [FromQuery] PagedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var teams = await _teamService.GetMineAsync(request, cancellationToken);
        return Ok(teams);
    }

    [HttpGet("{teamId:guid}")]
    public async Task<ActionResult<TeamResponse>> GetById(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var team = await _teamService.GetByIdAsync(teamId, cancellationToken);
        return Ok(team);
    }

    [HttpPut("{teamId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<TeamResponse>> Update(
        Guid teamId,
        UpdateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var team = await _teamService.UpdateAsync(
            teamId,
            request,
            cancellationToken);

        return Ok(team);
    }

    [HttpDelete("{teamId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        await _teamService.DeleteAsync(teamId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{teamId:guid}/members")]
    public async Task<ActionResult<PagedResponse<TeamMemberResponse>>> GetMembers(
        Guid teamId,
        [FromQuery] PagedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var members = await _teamService.GetMembersAsync(
            teamId,
            request,
            cancellationToken);

        return Ok(members);
    }

    [HttpPost("{teamId:guid}/members")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<TeamMemberResponse>> AddMember(
        Guid teamId,
        AddTeamMemberRequest request,
        CancellationToken cancellationToken)
    {
        var member = await _teamService.AddMemberAsync(
            teamId,
            request,
            cancellationToken);

        return Ok(member);
    }

    [HttpDelete("{teamId:guid}/members/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> RemoveMember(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _teamService.RemoveMemberAsync(
            teamId,
            userId,
            cancellationToken);

        return NoContent();
    }
}
