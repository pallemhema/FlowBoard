using FlowBoard.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBoard.Tests;

public sealed class WorkspaceOwnerComponentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task MissingOwnerNameRendersFallback(string? ownerName)
    {
        var html = await RenderOwnerAsync(new Dictionary<string, object?>
        {
            [nameof(WorkspaceOwner.OwnerName)] = ownerName
        });

        Assert.Contains("Owner information unavailable", html);
    }

    [Fact]
    public async Task OwnerDisplayRendersAvatarNameAndEmail()
    {
        var html = await RenderOwnerAsync(new Dictionary<string, object?>
        {
            [nameof(WorkspaceOwner.OwnerName)] = "Avery Owner",
            [nameof(WorkspaceOwner.OwnerEmail)] = "owner@example.test"
        });

        Assert.Contains("AO", html);
        Assert.Contains("Avery Owner", html);
        Assert.Contains("owner@example.test", html);
    }

    [Fact]
    public async Task MissingOwnerNameUsesEmailAndShowsNameFallback()
    {
        var html = await RenderOwnerAsync(new Dictionary<string, object?>
        {
            [nameof(WorkspaceOwner.OwnerEmail)] = "owner@example.test"
        });

        Assert.Contains("O", html);
        Assert.Contains("Name unavailable", html);
        Assert.Contains("owner@example.test", html);
    }

    [Fact]
    public async Task MissingOwnerEmailShowsEmailFallback()
    {
        var html = await RenderOwnerAsync(new Dictionary<string, object?>
        {
            [nameof(WorkspaceOwner.OwnerName)] = "Avery Owner"
        });

        Assert.Contains("Avery Owner", html);
        Assert.Contains("Email unavailable", html);
    }

    private static async Task<string> RenderOwnerAsync(
        Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<WorkspaceOwner>(
                ParameterView.FromDictionary(parameters));
            return component.ToHtmlString();
        });
    }
}
