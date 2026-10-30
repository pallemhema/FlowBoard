using FlowBoard.Api.Authorization;
using FlowBoard.Api.Services.Projects;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    public async Task<ActionResult<ProjectResponse>> Create(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projectService.CreateAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { projectId = project.ProjectId },
            project);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProjectResponse>>> Get(
        [FromQuery] ProjectQueryRequest request,
        CancellationToken cancellationToken)
    {
        var projects = await _projectService.GetAsync(
            request,
            cancellationToken);

        return Ok(projects);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResponse<ProjectResponse>>> GetMine(
        [FromQuery] ProjectQueryRequest request,
        CancellationToken cancellationToken)
    {
        var projects = await _projectService.GetMineAsync(
            request,
            cancellationToken);

        return Ok(projects);
    }

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectResponse>> GetById(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await _projectService.GetByIdAsync(
            projectId,
            cancellationToken);

        return Ok(project);
    }

    [HttpPut("{projectId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    public async Task<ActionResult<ProjectResponse>> Update(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projectService.UpdateAsync(
            projectId,
            request,
            cancellationToken);

        return Ok(project);
    }

    [HttpDelete("{projectId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    public async Task<IActionResult> Delete(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await _projectService.DeleteAsync(projectId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<ActionResult<PagedResponse<ProjectMemberResponse>>> GetMembers(
        Guid projectId,
        [FromQuery] PagedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var members = await _projectService.GetMembersAsync(
            projectId,
            request,
            cancellationToken);

        return Ok(members);
    }

    [HttpPost("{projectId:guid}/members")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    public async Task<ActionResult<ProjectMemberResponse>> AddMember(
        Guid projectId,
        AddProjectMemberRequest request,
        CancellationToken cancellationToken)
    {
        var member = await _projectService.AddMemberAsync(
            projectId,
            request,
            cancellationToken);

        return Ok(member);
    }

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    public async Task<IActionResult> RemoveMember(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _projectService.RemoveMemberAsync(
            projectId,
            userId,
            cancellationToken);

        return NoContent();
    }
}
