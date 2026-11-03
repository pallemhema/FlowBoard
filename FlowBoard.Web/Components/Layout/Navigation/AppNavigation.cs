using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using FlowBoard.Web.Authorization;

namespace FlowBoard.Web.Navigation;

public sealed record AppNavigationItem(
    string Label,
    string Icon,
    string? Href);

public sealed record AppNavigationSection(
    string Label,
    IReadOnlyList<AppNavigationItem> Items);

public static class AppNavigation
{
    private static readonly IReadOnlyList<AppNavigationSection> AdminSections =
    [
        new("Overview",
        [
            new("Dashboard", "bi-house-door", "/admin/dashboard"),
            new("Users", "bi-people", "/admin/users"),
            new("Teams", "bi-diagram-3", "/admin/teams"),
            new("Manager assignments", "bi-person-gear", "/admin/manager-assignments")
        ]),
        new("Work",
        [
            new("Workspaces", "bi-grid", "/admin/workspaces"),
            new("Projects", "bi-kanban", "/admin/projects"),
            new("Boards", "bi-columns-gap", "/admin/boards")
        ])
    ];

    private static readonly IReadOnlyList<AppNavigationSection> ManagerSections =
    [
        new("Overview",
        [
            new("Dashboard", "bi-house-door", "/manager/dashboard"),
            new("My assignments", "bi-person-lines-fill", "/manager/assignments")
        ]),
        new("Work",
        [
            new("My boards", "bi-columns-gap", "/manager/boards"),
            new("My teams", "bi-diagram-3", "/manager/teams"),
            new("My projects", "bi-kanban", "/manager/projects"),
            new("My workspaces", "bi-grid", "/manager/workspaces")
        ])
    ];

    private static readonly IReadOnlyList<AppNavigationSection> EmployeeSections =
    [
        new("Overview",
        [
            new("Dashboard", "bi-house-door", "/employee/dashboard"),
            new("My tasks", "bi-check2-square", "/employee/tasks"),
            new("My boards", "bi-columns-gap", "/employee/boards"),
            new("My projects", "bi-kanban", "/employee/projects")
        ]),
        new("Work",
        [
            new("My teams", "bi-diagram-3", "/employee/teams"),
            new("My workspaces", "bi-grid", "/employee/workspaces")
        ])
    ];

    public static IReadOnlyList<AppNavigationSection> ForAuthorizedPolicies(
        IReadOnlySet<string> authorizedPolicies)
    {
        if (authorizedPolicies.Contains(AuthorizationPolicies.AdminOnly))
        {
            return AdminSections;
        }

        if (authorizedPolicies.Contains(AuthorizationPolicies.ManagerOnly))
        {
            return ManagerSections;
        }

        return authorizedPolicies.Contains(AuthorizationPolicies.EmployeeOnly)
            ? EmployeeSections
            : [];
    }

    public static async Task<IReadOnlySet<string>> GetAuthorizedPoliciesAsync(
        ClaimsPrincipal user,
        IAuthorizationService authorizationService)
    {
        var authorizedPolicies = new HashSet<string>();
        foreach (var policy in new[]
                 {
                     AuthorizationPolicies.AdminOnly,
                     AuthorizationPolicies.ManagerOnly,
                     AuthorizationPolicies.EmployeeOnly
                 })
        {
            var result = await authorizationService.AuthorizeAsync(user, policy);
            if (result.Succeeded)
            {
                authorizedPolicies.Add(policy);
            }
        }

        return authorizedPolicies;
    }

   public static string DashboardHrefForPolicies(
       IReadOnlySet<string> authorizedPolicies) =>
       authorizedPolicies.Contains(AuthorizationPolicies.AdminOnly)
           ? "/admin/dashboard"
           : authorizedPolicies.Contains(AuthorizationPolicies.ManagerOnly)
               ? "/manager/dashboard"
               : authorizedPolicies.Contains(AuthorizationPolicies.EmployeeOnly)
                   ? "/employee/dashboard"
                   : "/login";
}
