using FlowBoard.Api.Authorization;
using FlowBoard.Api.Controllers;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.ManagerEmployees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FlowBoard.Tests;

public sealed class ControllerAuthorizationMetadataTests
{
    [Fact]
    public void UserDirectoryEndpointRequiresAdminOrManagerPolicy()
    {
        AssertMethodPolicy(
            typeof(AuthController),
            nameof(AuthController.GetUsers),
            AuthorizationPolicies.AdminOrManager);
    }

    [Fact]
    public void ProfileEndpointsUseAuthenticatedCurrentUserWithoutUserIdInput()
    {
        AssertMethodPolicy(
            typeof(AuthController),
            nameof(AuthController.Me),
            AuthorizationPolicies.AuthenticatedUser);
        AssertMethodPolicy(
            typeof(AuthController),
            nameof(AuthController.UpdateProfile),
            AuthorizationPolicies.AuthenticatedUser);

        Assert.DoesNotContain(
            typeof(AuthController).GetMethod(nameof(AuthController.Me))!
                .GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid));
        Assert.DoesNotContain(
            typeof(AuthController).GetMethod(nameof(AuthController.UpdateProfile))!
                .GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid));

        Assert.Equal(
            new[] { nameof(ProfileResponse.Email), nameof(ProfileResponse.FirstName),
                nameof(ProfileResponse.Id), nameof(ProfileResponse.LastName),
                nameof(ProfileResponse.Role) },
            typeof(ProfileResponse).GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ProvisioningAndRoleManagementEndpointsRequireAdminPolicy()
    {
        AssertMethodPolicy(
            typeof(AuthController),
            nameof(AuthController.CreateUser),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(AuthController),
            nameof(AuthController.PromoteEmployeeToManager),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(AuthController),
            nameof(AuthController.DeactivateUser),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(AuthController),
            nameof(AuthController.ReactivateUser),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(AuthController),
            nameof(AuthController.ReissueInvitation),
            AuthorizationPolicies.AdminOnly);

        var activationMethod = typeof(AuthController).GetMethod(
            nameof(AuthController.ActivateInvitation));
        Assert.NotNull(activationMethod);
        Assert.Single(
            activationMethod!.GetCustomAttributes(
                    typeof(AllowAnonymousAttribute),
                    inherit: true)
                .Cast<AllowAnonymousAttribute>());
        var rateLimit = Assert.Single(
            activationMethod.GetCustomAttributes(
                    typeof(EnableRateLimitingAttribute),
                    inherit: true)
                .Cast<EnableRateLimitingAttribute>());
        Assert.Equal("InvitationActivation", rateLimit.PolicyName);
    }

    [Fact]
    public void LegacyRegistrationEndpointIsAdminGatedAndDisabled()
    {
        AssertMethodPolicy(
            typeof(AuthController),
            nameof(AuthController.Register),
            AuthorizationPolicies.AdminOnly);
        Assert.Empty(
            typeof(AuthController)
                .GetMethod(nameof(AuthController.Register))!
                .GetCustomAttributes(
                    typeof(AllowAnonymousAttribute),
                    inherit: true));

        var controller = new AuthController(null!, null!);
        var result = controller.Register();

        Assert.Equal(
            StatusCodes.Status410Gone,
            Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public void ManagerEmployeeEndpointsUseCentralRolePolicies()
    {
        AssertMethodPolicy(
            typeof(ManagerEmployeesController),
            nameof(ManagerEmployeesController.Assign),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(ManagerEmployeesController),
            nameof(ManagerEmployeesController.GetManagedEmployees),
            AuthorizationPolicies.ManagerOnly);
        Assert.DoesNotContain(
            typeof(ManagerEmployeesController).GetMethods(),
            method => method.Name == "RemoveManagedEmployee");
        AssertMethodPolicy(
            typeof(ManagerEmployeesController),
            nameof(ManagerEmployeesController.GetMyManagers),
            AuthorizationPolicies.EmployeeOnly);
        AssertMethodPolicy(
            typeof(ManagerEmployeesController),
            nameof(ManagerEmployeesController.GetMyReportingManagers),
            AuthorizationPolicies.ManagerOnly);
        AssertMethodPolicy(
            typeof(ManagerEmployeesController),
            nameof(ManagerEmployeesController.GetMyReportingGroup),
            AuthorizationPolicies.EmployeeOnly);
        Assert.DoesNotContain(
            typeof(ManagerEmployeesController)
                .GetMethod(nameof(ManagerEmployeesController.GetMyReportingGroup))!
                .GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid));
        Assert.Equal(
            new[] { nameof(ReportingGroupMemberResponse.Email),
                nameof(ReportingGroupMemberResponse.EmployeeId),
                nameof(ReportingGroupMemberResponse.FirstName),
                nameof(ReportingGroupMemberResponse.LastName),
                nameof(ReportingGroupMemberResponse.Role) },
            typeof(ReportingGroupMemberResponse).GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
        AssertMethodPolicy(
            typeof(ManagerEmployeesController),
            nameof(ManagerEmployeesController.GetAll),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(ManagerEmployeesController),
            nameof(ManagerEmployeesController.Remove),
            AuthorizationPolicies.AdminOnly);
    }

    [Fact]
    public void ProjectAndTeamManagementEndpointsUseCentralRolePolicies()
    {
        AssertMethodPolicy(
            typeof(ProjectsController),
            nameof(ProjectsController.Create),
            AuthorizationPolicies.AdminOrManager);
        AssertMethodPolicy(
            typeof(ProjectsController),
            nameof(ProjectsController.Update),
            AuthorizationPolicies.AdminOrManager);
        AssertMethodPolicy(
            typeof(ProjectsController),
            nameof(ProjectsController.Delete),
            AuthorizationPolicies.AdminOrManager);
        AssertMethodPolicy(
            typeof(ProjectsController),
            nameof(ProjectsController.AddMember),
            AuthorizationPolicies.AdminOrManager);

        AssertMethodPolicy(
            typeof(TeamsController),
            nameof(TeamsController.Create),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(TeamsController),
            nameof(TeamsController.GetAll),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(TeamsController),
            nameof(TeamsController.Update),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(TeamsController),
            nameof(TeamsController.Delete),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(TeamsController),
            nameof(TeamsController.AddMember),
            AuthorizationPolicies.AdminOnly);
        AssertMethodPolicy(
            typeof(TeamsController),
            nameof(TeamsController.RemoveMember),
            AuthorizationPolicies.AdminOnly);
    }

    [Fact]
    public void WorkspaceManagementEndpointsUseAdminOrManagerPolicy()
    {
        AssertMethodPolicy(
            typeof(WorkspaceController),
            nameof(WorkspaceController.Create),
            AuthorizationPolicies.AdminOrManager);
        AssertMethodPolicy(
            typeof(WorkspaceController),
            nameof(WorkspaceController.Update),
            AuthorizationPolicies.AdminOrManager);
        AssertMethodPolicy(
            typeof(WorkspaceController),
            nameof(WorkspaceController.Delete),
            AuthorizationPolicies.AdminOrManager);
        AssertMethodPolicy(
            typeof(WorkspaceController),
            nameof(WorkspaceController.AddMember),
            AuthorizationPolicies.AdminOrManager);
        AssertMethodPolicy(
            typeof(WorkspaceController),
            nameof(WorkspaceController.RemoveMember),
            AuthorizationPolicies.AdminOrManager);
    }

    [Fact]
    public void TaskEndpointsRequireAuthenticatedUserPolicy()
    {
        var authorization = Assert.Single(
            typeof(TasksController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(AuthorizationPolicies.AuthenticatedUser, authorization.Policy);
    }

    private static void AssertMethodPolicy(
        Type controllerType,
        string methodName,
        string expectedPolicy)
    {
        var method = controllerType.GetMethod(methodName);
        Assert.NotNull(method);

        var authorization = Assert.Single(
            method!.GetCustomAttributes(
                    typeof(AuthorizeAttribute),
                    inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(expectedPolicy, authorization.Policy);
    }
}
