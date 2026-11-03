using FlowBoard.Web.Authorization;
using FlowBoard.Web.Components.Pages.Manager;
using FlowBoard.Web.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace FlowBoard.Tests;

public sealed class ManagerBoardsPageMetadataTests
{
    [Fact]
    public void ManagerBoardsRouteRequiresManagerPolicy()
    {
        var pageType = typeof(ManagerBoards);
        var route = Assert.Single(
            pageType.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                .Cast<RouteAttribute>());

        Assert.Equal("/manager/boards", route.Template);
        Assert.Contains(
            pageType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>(),
            attribute => attribute.Policy == AuthorizationPolicies.ManagerOnly);
    }

    [Fact]
    public void MyBoardsNavigationIsManagerOnlyAndInWorkSection()
    {
        var managerNavigation = AppNavigation.ForAuthorizedPolicies(
            new HashSet<string> { AuthorizationPolicies.ManagerOnly });
        var workSection = Assert.Single(
            managerNavigation.Where(section => section.Label == "Work"));
        Assert.Contains(
            workSection.Items,
            item => item.Label == "My boards" &&
                    item.Href == "/manager/boards");

        var adminLinks = AppNavigation.ForAuthorizedPolicies(
                new HashSet<string> { AuthorizationPolicies.AdminOnly })
            .SelectMany(section => section.Items)
            .Select(item => item.Href);
        Assert.DoesNotContain("/manager/boards", adminLinks);

        var employeeLinks = AppNavigation.ForAuthorizedPolicies(
                new HashSet<string> { AuthorizationPolicies.EmployeeOnly })
            .SelectMany(section => section.Items)
            .Select(item => item.Href);
        Assert.DoesNotContain("/manager/boards", employeeLinks);
    }
}
