using FlowBoard.Web.Authorization;
using FlowBoard.Web.Components.Pages.Manager;
using FlowBoard.Web.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace FlowBoard.Tests;

public sealed class ManagerProjectsPageMetadataTests
{
    [Fact]
    public void ManagerProjectRoutesRequireManagerPolicy()
    {
        var pageType = typeof(ManagerProjects);
        var routes = pageType
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(route => route.Template)
            .ToArray();

        Assert.Contains("/manager/projects", routes);
        Assert.Contains("/manager/projects/{Id:guid}", routes);
        Assert.Contains(
            pageType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>(),
            attribute => attribute.Policy == AuthorizationPolicies.ManagerOnly);
    }

    [Fact]
    public void ManagerNavigationIncludesMyProjectsWithoutChangingOtherRoleNavigation()
    {
        var managerPolicies = new HashSet<string> { AuthorizationPolicies.ManagerOnly };
        var managerHrefs = AppNavigation.ForAuthorizedPolicies(managerPolicies)
            .SelectMany(section => section.Items)
            .Select(item => item.Href)
            .ToArray();

        Assert.Contains("/manager/projects", managerHrefs);
        Assert.DoesNotContain("/admin/projects", managerHrefs);
        Assert.DoesNotContain("/employee/projects", managerHrefs);

        var adminPolicies = new HashSet<string>
        {
            AuthorizationPolicies.AdminOnly
        };
        var adminHrefs = AppNavigation.ForAuthorizedPolicies(adminPolicies)
            .SelectMany(section => section.Items)
            .Select(item => item.Href);
        Assert.Contains("/admin/projects", adminHrefs);
        Assert.DoesNotContain("/manager/projects", adminHrefs);

        var employeePolicies = new HashSet<string>
        {
            AuthorizationPolicies.EmployeeOnly
        };
        var employeeHrefs = AppNavigation.ForAuthorizedPolicies(employeePolicies)
            .SelectMany(section => section.Items)
            .Select(item => item.Href);
        Assert.Contains("/employee/projects", employeeHrefs);
        Assert.DoesNotContain("/manager/projects", employeeHrefs);
    }
}
