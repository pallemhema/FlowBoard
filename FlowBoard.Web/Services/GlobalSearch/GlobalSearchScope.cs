namespace FlowBoard.Web.Services.GlobalSearch;

public enum GlobalSearchEntity
{
    Users,
    Teams,
    Workspaces,
    Projects,
    Boards
}

public enum GlobalSearchSource
{
    UserDirectory,
    AllTeams,
    MyTeams,
    Workspaces,
    AllProjects,
    MyProjects,
    AccessibleBoards
}

public sealed record GlobalSearchCategory(
    GlobalSearchEntity Entity,
    GlobalSearchSource Source);

public static class GlobalSearchScope
{
    private static readonly IReadOnlyList<GlobalSearchCategory> AdminCategories =
    [
        new(GlobalSearchEntity.Users, GlobalSearchSource.UserDirectory),
        new(GlobalSearchEntity.Teams, GlobalSearchSource.AllTeams),
        new(GlobalSearchEntity.Workspaces, GlobalSearchSource.Workspaces),
        new(GlobalSearchEntity.Projects, GlobalSearchSource.AllProjects),
        new(GlobalSearchEntity.Boards, GlobalSearchSource.AccessibleBoards)
    ];

    private static readonly IReadOnlyList<GlobalSearchCategory> ManagerCategories =
    [
        new(GlobalSearchEntity.Teams, GlobalSearchSource.MyTeams),
        new(GlobalSearchEntity.Workspaces, GlobalSearchSource.Workspaces),
        new(GlobalSearchEntity.Projects, GlobalSearchSource.MyProjects),
        new(GlobalSearchEntity.Boards, GlobalSearchSource.AccessibleBoards)
    ];

    private static readonly IReadOnlyList<GlobalSearchCategory> EmployeeCategories =
    [
        new(GlobalSearchEntity.Teams, GlobalSearchSource.MyTeams),
        new(GlobalSearchEntity.Workspaces, GlobalSearchSource.Workspaces),
        new(GlobalSearchEntity.Projects, GlobalSearchSource.MyProjects),
        new(GlobalSearchEntity.Boards, GlobalSearchSource.AccessibleBoards)
    ];

    public static IReadOnlyList<GlobalSearchCategory> ForRole(string? role) =>
        role switch
        {
            "Admin" => AdminCategories,
            "Manager" => ManagerCategories,
            "Employee" => EmployeeCategories,
            _ => Array.Empty<GlobalSearchCategory>()
        };

    public static string? GetDestination(
        string? role,
        GlobalSearchEntity entity,
        Guid? resourceId = null,
        Guid? parentId = null)
    {
        return (role, entity) switch
        {
            ("Admin", GlobalSearchEntity.Users) => "/admin/users",
            ("Admin", GlobalSearchEntity.Teams) => "/admin/teams",
            ("Manager", GlobalSearchEntity.Teams) => "/manager/teams",
            ("Employee", GlobalSearchEntity.Teams) => "/employee/teams",
            ("Admin", GlobalSearchEntity.Workspaces) => "/admin/workspaces",
            ("Manager", GlobalSearchEntity.Workspaces) when resourceId.HasValue =>
                $"/manager/workspaces/{resourceId.Value}",
            ("Employee", GlobalSearchEntity.Workspaces) when resourceId.HasValue =>
                $"/employee/workspaces/{resourceId.Value}",
            ("Admin", GlobalSearchEntity.Projects) => "/admin/projects",
            ("Manager", GlobalSearchEntity.Projects) when resourceId.HasValue =>
                $"/manager/projects/{resourceId.Value}",
            ("Employee", GlobalSearchEntity.Projects) when resourceId.HasValue =>
                $"/employee/projects?projectId={resourceId.Value}",
            ("Admin" or "Manager" or "Employee", GlobalSearchEntity.Boards)
                when resourceId.HasValue && parentId.HasValue =>
                $"/workspaces/{parentId.Value}/boards/{resourceId.Value}",
            _ => null
        };
    }
}
