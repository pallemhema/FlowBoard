using FlowBoard.Api.Services.Workspaces;
using FlowBoard.Api.Authorization;
using FlowBoard.Shared.DTOs.Workspaces;
using FlowBoard.Shared.DTOs.Common;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceController(
        IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<WorkspaceResponse>> Create(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceService.CreateAsync(
            request,
            cancellationToken);

        return Ok(workspace);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<WorkspaceResponse>>> Get(
        [FromQuery] PagedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.GetAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkspaceResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceService.GetByIdAsync(
            id,
            cancellationToken);

        return Ok(workspace);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    public async Task<ActionResult<WorkspaceResponse>> Update(
        Guid id,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return Ok(workspace);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _workspaceService.DeleteAsync(
            id,
            cancellationToken);

        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<PagedResponse<WorkspaceMemberResponse>>> GetMembers(
        Guid id,
        [FromQuery] PagedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var members = await _workspaceService.GetMembersAsync(
            id,
            request,
            cancellationToken);

        return Ok(members);
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    public async Task<ActionResult<WorkspaceMemberResponse>> AddMember(
        Guid id,
        AddWorkspaceMemberRequest request,
        CancellationToken cancellationToken)
    {
        var member = await _workspaceService.AddMemberAsync(
            id,
            request,
            cancellationToken);

        return Ok(member);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    public async Task<IActionResult> RemoveMember(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _workspaceService.RemoveMemberAsync(
            id,
            userId,
            cancellationToken);

        return NoContent();
    }
}