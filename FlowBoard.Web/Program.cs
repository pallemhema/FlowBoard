using FlowBoard.Web.Components;
using FlowBoard.Web.Services.Api;
using FlowBoard.Web.Services.Authentication;
using FlowBoard.Web.Services.UI;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();


builder.Services.AddHttpContextAccessor();

// ============================================================
// HTTP CLIENT: FLOWBOARD WEB
//
// Used when Blazor calls the Web application's own
// AuthController endpoints:
//
//     POST /auth/refresh
//     POST /auth/logout
//
// ============================================================

builder.Services.AddHttpClient(
    "FlowBoardWeb",
    client =>
    {
        client.BaseAddress =
            new Uri("http://localhost:5174/");
    });

// ============================================================
// HTTP CLIENT: FLOWBOARD AUTH API
//
// Used when FlowBoard.Web calls the backend API:
//
//     POST /api/auth/login
//     POST /api/auth/register
//     POST /api/auth/refresh
//     POST /api/auth/logout
//
// ============================================================

builder.Services.AddHttpClient(
    "FlowBoardAuthApi",
    client =>
    {
        client.BaseAddress =
            new Uri("http://localhost:5062/");
    });



// ============================================================
// NORMAL BACKEND API CLIENT
//
// Example:
//
//     FlowBoardApi
//          ↓
//     AuthenticationHttpMessageHandler
//          ↓
//     http://localhost:5062
//
// ============================================================
builder.Services.AddHttpClient(
    "FlowBoardApi",
    client =>
    {
        client.BaseAddress =
            new Uri("http://localhost:5062/");
    });
    // .AddHttpMessageHandler<
    //     AuthenticationHttpMessageHandler>();

// ============================================================
// ASP.NET CORE AUTHENTICATION
//
// This cookie authentication is available for normal
// ASP.NET Core authentication middleware.
//
// Our Blazor authentication state is still handled by
// FlowBoardAuthenticationStateProvider.
// ============================================================

builder.Services
    .AddAuthentication("FlowBoardCookie")
    .AddCookie(
        "FlowBoardCookie",
        options =>
        {
            options.Cookie.Name =
                "FlowBoardAuth";

            options.LoginPath =
                "/login";

            options.AccessDeniedPath =
                "/access-denied";
        });

builder.Services.AddAuthorizationCore();

builder.Services.AddAuthorization();


// BLAZOR AUTHENTICATION STATE
// This makes AuthenticationState available to components
// through CascadingAuthenticationState.

builder.Services.AddCascadingAuthenticationState();



// AUTHENTICATION STATE
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<FlowBoardApiClient>();
builder.Services.AddScoped<IAccessTokenProvider,AccessTokenState>();

builder.Services.AddScoped<
    FlowBoardAuthenticationStateProvider>();

builder.Services.AddScoped<
    AuthenticationStateProvider>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            FlowBoardAuthenticationStateProvider>());

// AUTHENTICATION SERVICE

builder.Services.AddScoped<
    AuthenticationService>();

builder.Services.AddScoped<
    IAuthenticationService>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            AuthenticationService>());

// AUTHENTICATION HTTP HANDLER

builder.Services.AddScoped<
    AuthenticationHttpMessageHandler>();

// AUTH API SERVICE

builder.Services.AddScoped<
    IAuthApiService,
    AuthApiService>();

// PROFILE API SERVICE

builder.Services.AddScoped<
    ProfileApiService>();


builder.Services.AddScoped<
    IWorkspaceApiService,
    WorkspaceApiService>();

builder.Services.AddScoped<IBoardApiService, BoardApiService>();
builder.Services.AddScoped<IBoardColumnApiService, BoardColumnApiService>();
builder.Services.AddScoped<IBoardMemberApiService, BoardMemberApiService>();
builder.Services.AddScoped<IUserDirectoryApiService, UserDirectoryApiService>();

builder.Services.AddScoped<ICardApiService, CardApiService>();
builder.Services.AddScoped<ICardCommentApiService, CardCommentApiService>();
// BUILD APPLICATION

var app = builder.Build();

// HTTP PIPELINE

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}


app.UseHttpsRedirection();


app.UseStaticFiles();

app.UseAntiforgery();


app.UseAuthentication();



app.UseAuthorization();



app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// WEB API / MVC CONTROLLERS
//
// This maps:
//
//     /auth/login
//     /auth/refresh
//     /auth/logout
//
// ============================================================

app.MapControllers();


app.Run();