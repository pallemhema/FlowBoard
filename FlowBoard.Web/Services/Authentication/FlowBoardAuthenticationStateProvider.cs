// FlowBoardAuthenticationStateProvider.cs

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace FlowBoard.Web.Services.Authentication;

public class FlowBoardAuthenticationStateProvider
    : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous =
        new(new ClaimsIdentity());

    private ClaimsPrincipal _currentUser = Anonymous;

    public override Task<AuthenticationState>
        GetAuthenticationStateAsync()
    {
        return Task.FromResult(
            new AuthenticationState(_currentUser));
    }

    public void SetAuthenticatedUser(
        IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(
            claims,
            authenticationType: "FlowBoard");

        _currentUser =
            new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(
            Task.FromResult(
                new AuthenticationState(_currentUser)));
    }

    public void Logout()
    {
        _currentUser = Anonymous;

        NotifyAuthenticationStateChanged(
            Task.FromResult(
                new AuthenticationState(_currentUser)));
    }
}