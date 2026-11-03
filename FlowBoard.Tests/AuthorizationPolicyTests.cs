using System.Security.Claims;
using ApiAuthorizationPolicies = FlowBoard.Api.Authorization.AuthorizationPolicies;
using FlowBoard.Web.Authorization;
using FlowBoard.Web.Components.Pages.Admin;
using FlowBoard.Web.Components.Pages.Employee;
using FlowBoard.Web.Components.Pages.Manager;
using FlowBoard.Web.Components.Pages.Workspaces;
using FlowBoard.Web.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FlowBoard.Tests;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public void FrontendPolicyNamesMatchTheBackendPolicyNames()
    {
        Assert.Equal(
            ApiAuthorizationPolicies.AdminOnly,
            AuthorizationPolicies.AdminOnly);
        Assert.Equal(
            ApiAuthorizationPolicies.ManagerOnly,
            AuthorizationPolicies.ManagerOnly);
        Assert.Equal(
            ApiAuthorizationPolicies.EmployeeOnly,
            AuthorizationPolicies.EmployeeOnly);
        Assert.Equal(
            ApiAuthorizationPolicies.AdminOrManager,
            AuthorizationPolicies.AdminOrManager);
        Assert.Equal(
            ApiAuthorizationPolicies.AuthenticatedUser,
            AuthorizationPolicies.AuthenticatedUser);
    }

    [Theory]
    [InlineData("Admin", AuthorizationPolicies.AdminOnly)]
    [InlineData("Manager", AuthorizationPolicies.ManagerOnly)]
    [InlineData("Employee", AuthorizationPolicies.EmployeeOnly)]
    public async Task RolePolicyAllowsItsMatchingRole(
        string role,
        string policy)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(
            CreateUser(role),
            policy);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Manager", true)]
    [InlineData("Employee", false)]
    public async Task AdminOrManagerPolicyPreservesRoleAccess(
        string role,
        bool expected)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(
            CreateUser(role),
            AuthorizationPolicies.AdminOrManager);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Manager", false)]
    [InlineData("Employee", false)]
    public async Task AdminOnlyPolicyRejectsReportingRelationshipChangesByNonAdmins(
        string role,
        bool expected)
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(
            CreateUser(role),
            AuthorizationPolicies.AdminOnly);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task AuthenticatedUserPolicyRejectsAnonymousUsers()
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            AuthorizationPolicies.AuthenticatedUser);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void RestrictedPagesUseTheExpectedPolicies()
    {
        AssertPagePolicy(
            typeof(AdminDashboard),
            AuthorizationPolicies.AdminOnly);
        AssertPagePolicy(
            typeof(AdminTeams),
            AuthorizationPolicies.AdminOnly);
        AssertPagePolicy(
            typeof(AdminWorkspaces),
            AuthorizationPolicies.AdminOnly);
        AssertPagePolicy(
            typeof(AdminUsers),
            AuthorizationPolicies.AdminOnly);
        AssertPagePolicy(
            typeof(AdminBoards),
            AuthorizationPolicies.AdminOnly);
        AssertPagePolicy(
            typeof(AdminManagerAssignments),
            AuthorizationPolicies.AdminOnly);
        AssertPagePolicy(
            typeof(AdminProjects),
            AuthorizationPolicies.AdminOnly);
        AssertPagePolicy(
            typeof(EmployeeDashboard),
            AuthorizationPolicies.EmployeeOnly);
        AssertPagePolicy(
            typeof(EmployeeTasks),
            AuthorizationPolicies.EmployeeOnly);
        AssertPagePolicy(
            typeof(EmployeeBoards),
            AuthorizationPolicies.EmployeeOnly);
        AssertPagePolicy(
            typeof(EmployeeProjects),
            AuthorizationPolicies.EmployeeOnly);
        AssertPagePolicy(
            typeof(EmployeeTeams),
            AuthorizationPolicies.EmployeeOnly);
        AssertPagePolicy(
            typeof(EmployeeWorkspaces),
            AuthorizationPolicies.EmployeeOnly);
        AssertPagePolicy(
            typeof(EmployeeProfile),
            AuthorizationPolicies.EmployeeOnly);
        AssertPagePolicy(
            typeof(ManagerDashboard),
            AuthorizationPolicies.ManagerOnly);
        AssertPagePolicy(
            typeof(ManagerAssignments),
            AuthorizationPolicies.ManagerOnly);
        AssertPagePolicy(
            typeof(ManagerTeams),
            AuthorizationPolicies.ManagerOnly);
        AssertPagePolicy(
            typeof(ManagerWorkspaces),
            AuthorizationPolicies.ManagerOnly);
        AssertPagePolicy(
            typeof(WorkspaceCreate),
            AuthorizationPolicies.AdminOrManager);
        AssertPagePolicy(
            typeof(WorkspaceEdit),
            AuthorizationPolicies.AdminOrManager);
        AssertPagePolicy(
            typeof(WorkspaceDetails),
            AuthorizationPolicies.AuthenticatedUser);
    }

    [Theory]
    [InlineData("Admin", "/admin/dashboard")]
    [InlineData("Manager", "/manager/dashboard")]
    [InlineData("Employee", "/employee/dashboard")]
    public async Task NavigationAndLandingPageAreResolvedByPolicies(
        string role,
        string dashboardHref)
    {
        var user = CreateUser(role);
        var authorizedPolicies = await AppNavigation.GetAuthorizedPoliciesAsync(
            user,
            CreateAuthorizationService());

        Assert.Equal(
            dashboardHref,
            AppNavigation.DashboardHrefForPolicies(authorizedPolicies));

        var links = AppNavigation.ForAuthorizedPolicies(authorizedPolicies)
            .SelectMany(section => section.Items)
            .Where(item => item.Href is not null)
            .Select(item => item.Href)
            .ToArray();

        Assert.DoesNotContain(
            links,
            link => link is not null && link.StartsWith("/admin/", StringComparison.Ordinal) &&
                    role != "Admin");
        if (role == "Employee")
        {
            Assert.DoesNotContain("/employee/profile", links);
        }

        if (role == "Employee")
        {
            Assert.Collection(
                links,
                dashboard => Assert.Equal("/employee/dashboard", dashboard),
                tasks => Assert.Equal("/employee/tasks", tasks),
                boards => Assert.Equal("/employee/boards", boards),
                projects => Assert.Equal("/employee/projects", projects),
                teams => Assert.Equal("/employee/teams", teams),
                workspaces => Assert.Equal("/employee/workspaces", workspaces));
        }

        if (role == "Manager")
        {
            Assert.Collection(
                links,
                dashboard => Assert.Equal("/manager/dashboard", dashboard),
                assignments => Assert.Equal("/manager/assignments", assignments),
                boards => Assert.Equal("/manager/boards", boards),
                teams => Assert.Equal("/manager/teams", teams),
                projects => Assert.Equal("/manager/projects", projects),
                myWorkspaces => Assert.Equal("/manager/workspaces", myWorkspaces));
        }
    }

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(AuthorizationPolicies.Configure);
        return services.BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal CreateUser(string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "test"));

    private static void AssertPagePolicy(Type pageType, string expectedPolicy)
    {
        var authorization = Assert.Single(
            pageType.GetCustomAttributes(
                    typeof(AuthorizeAttribute),
                    inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(expectedPolicy, authorization.Policy);
    }
}
