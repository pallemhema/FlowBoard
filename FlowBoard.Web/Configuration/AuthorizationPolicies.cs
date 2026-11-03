using Microsoft.AspNetCore.Authorization;

namespace FlowBoard.Web.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ManagerOnly = nameof(ManagerOnly);
    public const string EmployeeOnly = nameof(EmployeeOnly);
    public const string AdminOrManager = nameof(AdminOrManager);
    public const string AuthenticatedUser = nameof(AuthenticatedUser);

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(
            AdminOnly,
            policy => policy.RequireRole("Admin"));

        options.AddPolicy(
            ManagerOnly,
            policy => policy.RequireRole("Manager"));

        options.AddPolicy(
            EmployeeOnly,
            policy => policy.RequireRole("Employee"));

        options.AddPolicy(
            AdminOrManager,
            policy => policy.RequireRole("Admin", "Manager"));

        options.AddPolicy(
            AuthenticatedUser,
            policy => policy.RequireAuthenticatedUser());
    }
}
