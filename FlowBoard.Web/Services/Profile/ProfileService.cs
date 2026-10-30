// // using System.Net;
// // using FlowBoard.Web.Services.Api;
// // using FlowBoard.Shared.DTOs.Auth;

// // using FlowBoard.Web.Services.UI;
// // namespace FlowBoard.Web.Services.Api;

// // public class ProfileApiService
// // {

// //     private readonly FlowBoardApiClient _apiClient;
// //     private readonly HttpClient _httpclient;

// //     private readonly ToastService _toastService;
// //     public ProfileApiService(
// //         IHttpClientFactory httpclient,
// //     FlowBoardApiClient apiClient,
// //     ToastService toastService)
// //     {
// //         _apiClient = apiClient;
// //             _toastService =
// //         toastService;
// //         _httpclient = httpclient.CreateClient("FlowBoardApi");
// //     }

// //     // ============================================================
// //     // GET PROFILE
// //     // ============================================================

// //     public async Task<ProfileResponse?> GetProfileAsync(
// //         CancellationToken cancellationToken = default)
// //     {
// //         Console.WriteLine();
// //         Console.WriteLine(
// //             "========== PROFILE API: GET PROFILE ==========");

// //         Console.WriteLine(
// //             "PROFILE API: Calling GET /api/auth/profile");

// //         var response = await _apiClient.GetAsync(
// //             "api/auth/profile",
// //             cancellationToken);

// //         Console.WriteLine(
// //             $"PROFILE API: Response = {(int)response.StatusCode} {response.StatusCode}");

// //         if (response.StatusCode == HttpStatusCode.Unauthorized)
// //         {
// //             Console.WriteLine(
// //                 "PROFILE API: Unauthorized.");

// //             return null;
// //         }

// //         if (!response.IsSuccessStatusCode)
// //         {
// //             Console.WriteLine(
// //                 "PROFILE API: Failed to load profile.");

// //             _toastService.Error(
// //     "Unable to load your profile.");

// //             return null;
// //         }

// //         var profile =
// //             await response.Content.ReadFromJsonAsync<ProfileResponse>(
// //                 cancellationToken);

// //         Console.WriteLine(
// //             $"PROFILE API: Profile received = {profile is not null}");

// //         return profile;
// //     }

// //     // ============================================================
// //     // UPDATE PROFILE
// //     // ============================================================

// //     public async Task<ProfileResponse?> UpdateProfileAsync(
// //         UpdateProfileRequest request,
// //         CancellationToken cancellationToken = default)
// //     {
// //         Console.WriteLine();
// //         Console.WriteLine(
// //             "========== PROFILE API: UPDATE PROFILE ==========");

// //         Console.WriteLine(
// //             "PROFILE API: Calling PUT /api/auth/profile");

// //         var response = await _apiClient.PutAsJsonAsync(
// //             "api/auth/profile",
// //             request,
// //             cancellationToken);

// //         Console.WriteLine(
// //             $"PROFILE API: Response = {(int)response.StatusCode} {response.StatusCode}");

// //         if (response.StatusCode == HttpStatusCode.Unauthorized)
// //         {
// //             Console.WriteLine(
// //                 "PROFILE API: Unauthorized.");

// //             return null;
// //         }

// //         if (!response.IsSuccessStatusCode)
// //         {
// //             Console.WriteLine(
// //                 "PROFILE API: Profile update failed.");

// //             _toastService.Error(
// //     "Profile updated Failed.");

// //             return null;
// //         }

// //         var profile =
// //             await response.Content.ReadFromJsonAsync<ProfileResponse>(
// //                 cancellationToken);

// //         Console.WriteLine(
// //             $"PROFILE API: Updated profile received = {profile is not null}");
// //             _toastService.Success(
// //     "Profile updated successfully.");

// //         return profile;
// //     }
// // }

// using System.Net;
// using System.Net.Http.Json;
// using FlowBoard.Shared.DTOs.Auth;
// using FlowBoard.Web.Services.UI;

// namespace FlowBoard.Web.Services.Api;

// public class ProfileApiService
// {
//     private readonly FlowBoardApiClient _apiClient;
//     private readonly ToastService _toastService;

//     public ProfileApiService(
//         FlowBoardApiClient apiClient,
//         ToastService toastService)
//     {
//         _apiClient = apiClient;
//         _toastService = toastService;
//     }

//     public async Task<ProfileResponse?> GetProfileAsync(
//         CancellationToken cancellationToken = default)
//     {
//         Console.WriteLine();
//         Console.WriteLine(
//             "========== PROFILE API: GET PROFILE ==========");

//         Console.WriteLine(
//             "PROFILE API: Calling GET /api/auth/profile");

//         var response =
//             await _apiClient.GetAsync(
//                 "api/auth/profile",
//                 cancellationToken);

//         Console.WriteLine(
//             $"PROFILE API: Response = " +
//             $"{(int)response.StatusCode} {response.StatusCode}");

//         if (response.StatusCode ==
//             HttpStatusCode.Unauthorized)
//         {
//             Console.WriteLine(
//                 "PROFILE API: Unauthorized.");

//             return null;
//         }

//         if (!response.IsSuccessStatusCode)
//         {
//             Console.WriteLine(
//                 "PROFILE API: Failed to load profile.");

//             _toastService.Error(
//                 "Unable to load your profile.");

//             return null;
//         }

//         var profile =
//             await response.Content.ReadFromJsonAsync<ProfileResponse>(
//                 cancellationToken);

//         Console.WriteLine(
//             $"PROFILE API: Profile received = " +
//             $"{profile is not null}");

//         return profile;
//     }

//     public async Task<ProfileResponse?> UpdateProfileAsync(
//         UpdateProfileRequest request,
//         CancellationToken cancellationToken = default)
//     {
//         Console.WriteLine();
//         Console.WriteLine(
//             "========== PROFILE API: UPDATE PROFILE ==========");

//         Console.WriteLine(
//             "PROFILE API: Calling PUT /api/auth/profile");

//         var response =
//             await _apiClient.PutAsJsonAsync(
//                 "api/auth/profile",
//                 request,
//                 cancellationToken);

//         Console.WriteLine(
//             $"PROFILE API: Response = " +
//             $"{(int)response.StatusCode} {response.StatusCode}");

//         if (response.StatusCode ==
//             HttpStatusCode.Unauthorized)
//         {
//             Console.WriteLine(
//                 "PROFILE API: Unauthorized.");

//             return null;
//         }

//         if (!response.IsSuccessStatusCode)
//         {
//             Console.WriteLine(
//                 "PROFILE API: Profile update failed.");

//             _toastService.Error(
//                 "Profile update failed.");

//             return null;
//         }

//         var profile =
//             await response.Content.ReadFromJsonAsync<ProfileResponse>(
//                 cancellationToken);

//         Console.WriteLine(
//             $"PROFILE API: Updated profile received = " +
//             $"{profile is not null}");

//         _toastService.Success(
//             "Profile updated successfully.");

//         return profile;
//     }
// }

using System.Net;
using System.Net.Http.Json;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Web.Services.UI;

namespace FlowBoard.Web.Services.Api;

public class ProfileApiService
{
    private readonly FlowBoardApiClient _apiClient;
    private readonly ToastService _toastService;

    public ProfileApiService(
        FlowBoardApiClient apiClient,
        ToastService toastService)
    {
        _apiClient = apiClient;
        _toastService = toastService;
    }

    // ============================================================
    // GET PROFILE
    // ============================================================

    public async Task<ProfileResponse?> GetProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            "api/auth/profile",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to load your profile.");

            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProfileResponse>(
            cancellationToken);
    }

    // ============================================================
    // UPDATE PROFILE
    // ============================================================

    public async Task<ProfileResponse?> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PutAsJsonAsync(
            "api/auth/profile",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Profile update failed.");

            return null;
        }

        var profile =
            await response.Content.ReadFromJsonAsync<ProfileResponse>(
                cancellationToken);

        if (profile is not null)
        {
            _toastService.Success(
                "Profile updated successfully.");
        }

        return profile;
    }
}