using FlowBoard.Api.Authorization;
using FlowBoard.Api.Services.ManagerEmployees;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.ManagerEmployees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Route("api/manager-employees")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class ManagerEmployeesController : ControllerBase
{
    private readonly IManagerEmployeeService _managerEmployeeService;

    public ManagerEmployeesController(
        IManagerEmployeeService managerEmployeeService)
    {
        _managerEmployeeService = managerEmployeeService;
    }

    [HttpPost("managers/{managerId:guid}/employees")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ManagerEmployeeResponse>> Assign(
        Guid managerId,
        AssignEmployeeToManagerRequest request,
        CancellationToken cancellationToken)
    {
        var relationship = await _managerEmployeeService.AssignAsync(
            managerId,
            request,
            cancellationToken);

        return Ok(relationship);
    }

    [HttpGet("mine/employees")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOnly)]
    public async Task<ActionResult<PagedResponse<ManagerEmployeeResponse>>>
        GetManagedEmployees(
            [FromQuery] PagedRequest request,
            CancellationToken cancellationToken)
    {
        var employees = await _managerEmployeeService.GetManagedEmployeesAsync(
            request,
            cancellationToken);

        return Ok(employees);
    }

    [HttpGet("mine/managers")]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOnly)]
    public async Task<ActionResult<PagedResponse<ManagerEmployeeResponse>>>
        GetMyManagers(
            [FromQuery] PagedRequest request,
            CancellationToken cancellationToken)
    {
        var managers = await _managerEmployeeService.GetMyManagersAsync(
            request,
            cancellationToken);

        return Ok(managers);
    }

    [HttpGet("mine/reporting-managers")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOnly)]
    public async Task<ActionResult<PagedResponse<ManagerEmployeeResponse>>>
        GetMyReportingManagers(
            [FromQuery] PagedRequest request,
            CancellationToken cancellationToken)
    {
        var managers = await _managerEmployeeService.GetMyReportingManagersAsync(
            request,
            cancellationToken);

        return Ok(managers);
    }

    [HttpGet("mine/reporting-group")]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOnly)]
    public async Task<ActionResult<PagedResponse<ReportingGroupMemberResponse>>>
        GetMyReportingGroup(
            [FromQuery] PagedRequest request,
            CancellationToken cancellationToken)
    {
        var members = await _managerEmployeeService.GetMyReportingGroupAsync(
            request,
            cancellationToken);

        return Ok(members);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<PagedResponse<ManagerEmployeeResponse>>> GetAll(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var relationships = await _managerEmployeeService.GetAllAsync(
            request,
            cancellationToken);

        return Ok(relationships);
    }

    [HttpDelete("managers/{managerId:guid}/employees/{employeeId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Remove(
        Guid managerId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        await _managerEmployeeService.RemoveAsync(
            managerId,
            employeeId,
            cancellationToken);

        return NoContent();
    }
}
