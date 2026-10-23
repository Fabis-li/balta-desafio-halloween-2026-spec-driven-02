using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Application;
using LinkShortener.Api.Endpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LinkShortener.Tests.Integration;

public class StatsTests(LinkShortenerFactory factory) : IClassFixture<LinkShortenerFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task GetStats_WithKnownCode_Returns200WithTotalAccesses()
    {
        var code = await CreateLinkAsync("ESTATISTICA");

        for (var i = 0; i < 3; i++)
            await _client.GetAsync($"/{code}");

        var response = await _client.GetAsync($"/api/links/{code}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stats = await response.Content.ReadFromJsonAsync<LinkStatsResponse>();
        Assert.NotNull(stats);
        Assert.Equal(code, stats.Code);
        Assert.Equal(3, stats.AccessCount);
    }

    [Fact]
    public async Task GetStats_WithoutAnyCredential_IsAllowed()
    {
        var code = await CreateLinkAsync("PUBLICO");

        using var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/links/{code}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<LinkStatsResponse>())!.AccessCount);
    }

    [Fact]
    public async Task GetStats_DoesNotCountAsAnAccess()
    {
        var code = await CreateLinkAsync("SOLEITURA");

        await _client.GetAsync($"/api/links/{code}/stats");
        var response = await _client.GetAsync($"/api/links/{code}/stats");

        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<LinkStatsResponse>())!.AccessCount);
    }

    [Fact]
    public async Task GetStats_WithUnknownCode_Returns404WithProblemDetails()
    {
        var response = await _client.GetAsync("/api/links/NAOEXISTE/stats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(GetLinkStatsResult.NotFound.Error, problem.Detail);
    }

    private async Task<string> CreateLinkAsync(string code)
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { targetUrl = "https://example.com", code });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateLinkResponse>())!.Code;
    }
}
