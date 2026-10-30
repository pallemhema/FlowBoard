using FlowBoard.Infrastructure;
using FlowBoard.Infrastructure.Data;
using FlowBoard.Api.Data;
using FlowBoard.Api.Services.Authentication;
using FlowBoard.Api.Configuration;
using FlowBoard.Api.Middleware;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Api.Services.Workspaces;
using FlowBoard.Api.Services.Boards;
using FlowBoard.Api.Services.BoardMembers;
using FlowBoard.Api.Services.BoardColumns;
using FlowBoard.Api.Services.Cards;
using FlowBoard.Api.Services.CardComments;
using FlowBoard.Api.Services.Teams;
using FlowBoard.Api.Services.ManagerEmployees;
using FlowBoard.Api.Services.Projects;
using FlowBoard.Api.Services.BoardAuthorization;
using FlowBoard.Api.Services.TaskAssignments;
using FlowBoard.Api.Services.MyTasks;
using FlowBoard.Api.Authorization;
using FlowBoard.Domain.Enums;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration);
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "JWT configuration is missing.");

jwtSettings.Validate();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = jwtSettings.CreateSigningKey(),

            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateLifetime = true,

            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,

            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthorizationPolicies.AdminOnly,
        policy => policy.RequireRole(nameof(UserRole.Admin)));

    options.AddPolicy(
        AuthorizationPolicies.ManagerOnly,
        policy => policy.RequireRole(nameof(UserRole.Manager)));

    options.AddPolicy(
        AuthorizationPolicies.EmployeeOnly,
        policy => policy.RequireRole(nameof(UserRole.Employee)));

    options.AddPolicy(
        AuthorizationPolicies.AdminOrManager,
        policy => policy.RequireRole(
            nameof(UserRole.Admin),
            nameof(UserRole.Manager)));

    options.AddPolicy(
        AuthorizationPolicies.AuthenticatedUser,
        policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description =
                "Enter your JWT access token."
        });

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type =
                            Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IBoardService, BoardService>();
builder.Services.AddScoped<IBoardMemberService, BoardMemberService>();
builder.Services.AddScoped<IBoardColumnService, BoardColumnService>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddScoped<ICardCommentService, CardCommentService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IManagerEmployeeService, ManagerEmployeeService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IBoardAuthorizationService, BoardAuthorizationService>();
builder.Services.AddScoped<ITaskAssignmentGuard, TaskAssignmentGuard>();
builder.Services.AddScoped<IMyTasksService, MyTasksService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<FlowBoardDbContext>();

    var passwordHasher = scope.ServiceProvider
        .GetRequiredService<IPasswordHasher>();

    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DevelopmentDataSeeder");

    var seedSettings = app.Configuration
        .GetSection("DevelopmentSeed")
        .Get<DevelopmentSeedSettings>() ?? new DevelopmentSeedSettings();

    await DevelopmentDataSeeder.SeedAsync(
        dbContext,
        passwordHasher,
        logger,
        seedSettings,
        app.Environment);
}
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
