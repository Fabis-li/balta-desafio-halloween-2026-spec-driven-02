using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Tests.Integration;

public class RedirectCacheTests(LinkShortenerFactory factory) : IClassFixture<LinkShortenerFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Get_SecondRedirectOfSameCode_DoesNotReadTheLinkFromTheDatabase()
    {
        await CreateLinkAsync("CACHE1", null);

        await _client.GetAsync("/CACHE1");
        var selectsAfterFirst = factory.Selects.Count;

        var response = await _client.GetAsync("/CACHE1");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(selectsAfterFirst, factory.Selects.Count);
    }

    [Fact]
    public async Task Get_CachedRedirects_StillCountEveryAccess()
    {
        await CreateLinkAsync("CACHE2", null);

        for (var i = 0; i < 10; i++)
            await _client.GetAsync("/CACHE2");

        var stats = await _client.GetFromJsonAsync<LinkStatsResponse>("/api/links/CACHE2/stats");
        Assert.Equal(10, stats!.AccessCount);
    }

    [Fact]
    public async Task Get_CachedLinkAfterExpiration_Returns410WithoutCounting()
    {
        await CreateLinkAsync("CACHE3", "2026-12-31");

        Assert.Equal(HttpStatusCode.Found, (await _client.GetAsync("/CACHE3")).StatusCode);
        var selectsWhileCached = factory.Selects.Count;

        factory.Clock.Now = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
        try
        {
            var response = await _client.GetAsync("/CACHE3");

            Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
            Assert.Equal(selectsWhileCached, factory.Selects.Count);
        }
        finally
        {
            factory.Clock.Now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        }

        var accessCount = await factory.QueryDatabaseAsync(db =>
            db.Links.Where(link => link.Code == "CACHE3").Select(link => link.AccessCount).SingleAsync());
        Assert.Equal(1, accessCount);
    }

    private async Task CreateLinkAsync(string code, string? expiresOn)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/links", new { targetUrl = "https://example.com", code, expiresOn });
        response.EnsureSuccessStatusCode();
    }
}
