using System.Security.Claims;

using FlowBoard.Api.Exceptions;
using FlowBoard.Infrastructure.Data;

namespace FlowBoard.Api.Services.CurrentUser;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated
        ?? false;

    public Guid UserId
    {
        get
        {
            var userId = _httpContextAccessor.HttpContext?
                .User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userId, out var id))
            {
                throw new UnauthorizedException(
                    "Authenticated user ID could not be determined.");
            }

            return id;
        }
    }

    public string? Email =>
        _httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.Email);

    public string? Role =>
        _httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.Role);
}