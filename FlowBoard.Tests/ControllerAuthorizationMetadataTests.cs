using FlowBoard.Api.Authorization;
using FlowBoard.Api.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace FlowBoard.Tests;

public sealed class ControllerAuthorizationMetadataTests
{
    [Fact]
    public void UserDirectoryEndpointRequiresAdminOrManagerPolicy()
    {
        var method = typeof(AuthController).GetMethod(
            nameof(AuthController.GetUsers));

        Assert.NotNull(method);
        var authorization = Assert.Single(
            method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(AuthorizationPolicies.AdminOrManager, authorization.Policy);
    }
}
