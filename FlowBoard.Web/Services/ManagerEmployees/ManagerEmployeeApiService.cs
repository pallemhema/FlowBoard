using System.Net;
using System.Net.Http.Json;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.ManagerEmployees;
using FlowBoard.Web.Services.UI;

namespace FlowBoard.Web.Services.Api;

public sealed class ManagerEmployeeApiService : IManagerEmployeeApiService
{
    private const string Root = "api/manager-employees";
    private readonly FlowBoardApiClient _apiClient;
    private readonly ToastService _toastService;

    public ManagerEmployeeApiService(
        FlowBoardApiClient apiClient,
        ToastService toastService)
    {
        _apiClient = apiClient;
        _toastService = toastService;
    }

    public Task<PagedResponse<ManagerEmployeeResponse>?> GetAllAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<ManagerEmployeeResponse>(
            $"{Root}{PageQuery(request)}",
            cancellationToken);

    public Task<PagedResponse<ManagerEmployeeResponse>?> GetManagedEmployeesAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<ManagerEmployeeResponse>(
            $"{Root}/mine/employees{PageQuery(request)}",
            cancellationToken);

    public Task<PagedResponse<ManagerEmployeeResponse>?> GetMyManagersAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<ManagerEmployeeResponse>(
            $"{Root}/mine/managers{PageQuery(request)}",
            cancellationToken);

    public Task<PagedResponse<ManagerEmployeeResponse>?> GetMyReportingManagersAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<ManagerEmployeeResponse>(
            $"{Root}/mine/reporting-managers{PageQuery(request)}",
            cancellationToken);

    public Task<PagedResponse<ReportingGroupMemberResponse>?> GetMyReportingGroupAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<ReportingGroupMemberResponse>(
            $"{Root}/mine/reporting-group{PageQuery(request)}",
            cancellationToken);

    public async Task<ManagerEmployeeResponse?> AssignAsync(
        Guid managerId,
        AssignEmployeeToManagerRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            $"{Root}/managers/{managerId}/employees",
            request,
            cancellationToken);

        return await ReadAsync<ManagerEmployeeResponse>(
            response,
            "Unable to assign the employee to the Manager.",
            cancellationToken,
            validationMessage: "The selected account must have the Manager role, and only Employee accounts can be assigned.",
            conflictMessage: "This employee is already assigned to this manager.");
    }

    public async Task<bool> RemoveAsync(
        Guid managerId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"{Root}/managers/{managerId}/employees/{employeeId}",
            cancellationToken);

        return ReportSuccess(response, "Unable to remove the employee assignment.");
    }

    private async Task<PagedResponse<T>?> GetPageAsync<T>(
        string url,
        CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetAsync(url, cancellationToken);
        return await ReadAsync<PagedResponse<T>>(
            response,
            "Unable to load Manager-Employee relationships.",
            cancellationToken);
    }

    private async Task<T?> ReadAsync<T>(
        HttpResponseMessage response,
        string errorMessage,
        CancellationToken cancellationToken,
        string? validationMessage = null,
        string? conflictMessage = null)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                await ReportFailureAsync(
                    response,
                    errorMessage,
                    cancellationToken,
                    validationMessage,
                    conflictMessage);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(
                cancellationToken);
        }
    }

    private bool ReportSuccess(
        HttpResponseMessage response,
        string errorMessage)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _toastService.Error(GetStatusMessage(response.StatusCode, errorMessage));
            return false;
        }
    }

    private async Task ReportFailureAsync(
        HttpResponseMessage response,
        string errorMessage,
        CancellationToken cancellationToken,
        string? validationMessage = null,
        string? conflictMessage = null)
    {
        var fallbackMessage = GetStatusMessage(
            response.StatusCode,
            errorMessage,
            validationMessage,
            conflictMessage);

        try
        {
            var apiError = await response.Content
                .ReadFromJsonAsync<ErrorResponse>(cancellationToken);
            _toastService.Error(
                string.IsNullOrWhiteSpace(apiError?.Message)
                    ? fallbackMessage
                    : apiError.Message);
        }
        catch (System.Text.Json.JsonException)
        {
            _toastService.Error(fallbackMessage);
        }
    }

    private static string GetStatusMessage(
        HttpStatusCode statusCode,
        string fallback,
        string? validationMessage = null,
        string? conflictMessage = null) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Your session is no longer authorized. Sign in again and retry.",
            HttpStatusCode.Forbidden =>
                "You are not authorized to manage Manager-Employee assignments.",
            HttpStatusCode.NotFound =>
                "The manager, employee, or assignment could not be found.",
            HttpStatusCode.Conflict =>
                conflictMessage ?? "This employee is already assigned to the manager.",
            HttpStatusCode.BadRequest =>
                validationMessage ?? "The assignment request is invalid.",
            _ => fallback
        };

    private static string PageQuery(PagedRequest request) =>
        $"?Page={request.Page}&PageSize={request.PageSize}";
}
