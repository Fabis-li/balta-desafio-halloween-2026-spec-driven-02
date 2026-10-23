using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Application;
using LinkShortener.Api.Endpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LinkShortener.Tests.Integration;

public class RedirectTests(LinkShortenerFactory factory) : IClassFixture<LinkShortenerFactory>
{
    // Sem seguir o redirect, para o teste enxergar o 302
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Get_WithExistingCode_Returns302ToTargetUrl()
    {
        var created = await CreateLinkAsync("https://example.com/destino?origem=teste");

        var response = await _client.GetAsync($"/{created.Code}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(new Uri("https://example.com/destino?origem=teste"), response.Headers.Location);
    }

    [Fact]
    public async Task Get_UsingTheReturnedShortUrl_Returns302ToTargetUrl()
    {
        var created = await CreateLinkAsync("https://example.com/curto", "CURTO");

        var response = await _client.GetAsync(created.ShortUrl);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(new Uri("https://example.com/curto"), response.Headers.Location);
    }

    [Fact]
    public async Task Get_WithUnknownCode_Returns404WithProblemDetails()
    {
        var response = await _client.GetAsync("/NAOEXISTE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(ResolveLinkResult.NotFound.Error, problem.Detail);
    }

    [Fact]
    public async Task Get_WithLowercaseVersionOfExistingCode_Returns404()
    {
        await CreateLinkAsync("https://example.com/maiusculo", "MAIUSCULO");

        var response = await _client.GetAsync("/maiusculo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<CreateLinkResponse> CreateLinkAsync(string url, string? code = null)
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { targetUrl = url, code });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateLinkResponse>())!;
    }
}
