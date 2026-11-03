using FlowBoard.Web.Services.GlobalSearch;
using FlowBoard.Web.Components.Layout;
using FlowBoard.Web.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace FlowBoard.Tests;

public class GlobalSearchScopeTests
{
    [Fact]
    public void AdminSearchUsesAdministrativeDirectories()
    {
        var categories = GlobalSearchScope.ForRole("Admin");

        Assert.Collection(
            categories,
            category => Assert.Equal(
                (GlobalSearchEntity.Users, GlobalSearchSource.UserDirectory),
                (category.Entity, category.Source)),
            category => Assert.Equal(
                (GlobalSearchEntity.Teams, GlobalSearchSource.AllTeams),
                (category.Entity, category.Source)),
            category => Assert.Equal(
                (GlobalSearchEntity.Workspaces, GlobalSearchSource.Workspaces),
                (category.Entity, category.Source)),
            category => Assert.Equal(
                (GlobalSearchEntity.Projects, GlobalSearchSource.AllProjects),
                (category.Entity, category.Source)),
            category => Assert.Equal(
                (GlobalSearchEntity.Boards, GlobalSearchSource.AccessibleBoards),
                (category.Entity, category.Source)));
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Employee")]
    public void NonAdminSearchUsesScopedResourcesAndOmitsUserDirectory(string role)
    {
        var categories = GlobalSearchScope.ForRole(role);

        Assert.DoesNotContain(
            categories,
            category => category.Source is GlobalSearchSource.UserDirectory
                or GlobalSearchSource.AllTeams
                or GlobalSearchSource.AllProjects);
        Assert.Equal(
            new[]
            {
                GlobalSearchEntity.Teams,
                GlobalSearchEntity.Workspaces,
                GlobalSearchEntity.Projects,
                GlobalSearchEntity.Boards
            },
            categories.Select(category => category.Entity));
        Assert.Equal(
            new[]
            {
                GlobalSearchSource.MyTeams,
                GlobalSearchSource.Workspaces,
                GlobalSearchSource.MyProjects,
                GlobalSearchSource.AccessibleBoards
            },
            categories.Select(category => category.Source));
    }

    [Fact]
    public void UnknownRoleHasNoSearchCategories()
    {
        Assert.Empty(GlobalSearchScope.ForRole("Unknown"));
        Assert.Null(
            GlobalSearchScope.GetDestination(
                "Unknown",
                GlobalSearchEntity.Boards,
                Guid.NewGuid(),
                Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Admin", GlobalSearchEntity.Teams, "/admin/teams")]
    [InlineData("Manager", GlobalSearchEntity.Teams, "/manager/teams")]
    [InlineData("Employee", GlobalSearchEntity.Teams, "/employee/teams")]
    [InlineData("Admin", GlobalSearchEntity.Workspaces, "/admin/workspaces")]
    public void SearchDestinationsUseRoleSpecificListRoutes(
        string role,
        GlobalSearchEntity entity,
        string expected)
    {
        Assert.Equal(expected, GlobalSearchScope.GetDestination(role, entity));
    }

    [Fact]
    public void SearchDestinationsUseExistingAuthorizedDetailsRoutes()
    {
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var boardId = Guid.NewGuid();

        Assert.Equal(
            $"/manager/workspaces/{workspaceId}",
            GlobalSearchScope.GetDestination(
                "Manager",
                GlobalSearchEntity.Workspaces,
                workspaceId));
        Assert.Equal(
            $"/manager/projects/{projectId}",
            GlobalSearchScope.GetDestination(
                "Manager",
                GlobalSearchEntity.Projects,
                projectId));
        Assert.Equal(
            $"/employee/projects?projectId={projectId}",
            GlobalSearchScope.GetDestination(
                "Employee",
                GlobalSearchEntity.Projects,
                projectId));
        Assert.Equal(
            $"/workspaces/{workspaceId}/boards/{boardId}",
            GlobalSearchScope.GetDestination(
                "Employee",
                GlobalSearchEntity.Boards,
                boardId,
                workspaceId));
    }

    [Fact]
    public void RoleWithoutScopedDetailsRouteDoesNotReceiveOne()
    {
        Assert.Null(
            GlobalSearchScope.GetDestination(
                "Manager",
                GlobalSearchEntity.Users,
                Guid.NewGuid()));
        Assert.Null(
            GlobalSearchScope.GetDestination(
                "Employee",
                GlobalSearchEntity.Workspaces));
    }

    [Fact]
    public void SharedSearchComponentExposesLayoutIntegrationContract()
    {
        Assert.Equal(typeof(GlobalSearch).Assembly, typeof(MainLayout).Assembly);
        Assert.NotNull(
            typeof(GlobalSearch).GetProperty(nameof(GlobalSearch.SearchStateChanged)));
        Assert.Contains(
            typeof(GlobalSearch).GetProperty(nameof(GlobalSearch.SearchStateChanged))!
                .GetCustomAttributes(typeof(ParameterAttribute), inherit: true),
            attribute => attribute is ParameterAttribute);
        Assert.Equal(typeof(Task), typeof(GlobalSearch).GetMethod(nameof(GlobalSearch.CloseAsync))?.ReturnType);
    }
}
