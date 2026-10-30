using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Web.Services.Api;

public interface IUserDirectoryApiService
{
    Task<PagedResponse<UserDirectoryResponse>?> GetAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);
}
