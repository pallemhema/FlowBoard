using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.ManagerEmployees;

namespace FlowBoard.Web.Services.Api;

public interface IManagerEmployeeApiService
{
    Task<PagedResponse<ManagerEmployeeResponse>?> GetAllAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ManagerEmployeeResponse>?> GetManagedEmployeesAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ManagerEmployeeResponse>?> GetMyManagersAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ManagerEmployeeResponse>?> GetMyReportingManagersAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ReportingGroupMemberResponse>?> GetMyReportingGroupAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<ManagerEmployeeResponse?> AssignAsync(
        Guid managerId,
        AssignEmployeeToManagerRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(
        Guid managerId,
        Guid employeeId,
        CancellationToken cancellationToken = default);
}
