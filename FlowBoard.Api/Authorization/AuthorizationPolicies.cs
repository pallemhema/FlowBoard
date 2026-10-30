namespace FlowBoard.Api.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);

    public const string ManagerOnly = nameof(ManagerOnly);

    public const string EmployeeOnly = nameof(EmployeeOnly);

    public const string AdminOrManager = nameof(AdminOrManager);

    public const string AuthenticatedUser = nameof(AuthenticatedUser);
}
