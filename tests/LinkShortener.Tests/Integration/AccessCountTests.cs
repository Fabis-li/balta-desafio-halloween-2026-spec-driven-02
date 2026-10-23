using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Tests.Integration;

public class AccessCountTests(LinkShortenerFactory factory) : IClassFixture<LinkShortenerFactory>
{
    private const int ConcurrentAccesses = 200;

    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Get_RepeatedAccesses_IncrementsCounterEachTime()
    {
        var code = await CreateLinkAsync("REPETIDO");

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Found, (await _client.GetAsync($"/{code}")).StatusCode);

        Assert.Equal(5, await ReadAccessCountAsync(code));
    }

    [Fact]
    public async Task Get_ConcurrentAccesses_DoNotLoseAnyCount()
    {
        var code = await CreateLinkAsync("CONCORRENTE");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentAccesses).Select(_ => _client.GetAsync($"/{code}")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Found, response.StatusCode));
        Assert.Equal(ConcurrentAccesses, await ReadAccessCountAsync(code));
    }

    [Fact]
    public async Task Get_UnknownCode_DoesNotChangeOtherCounters()
    {
        var code = await CreateLinkAsync("INTACTO");

        await _client.GetAsync("/NAOEXISTE");

        Assert.Equal(0, await ReadAccessCountAsync(code));
    }

    private async Task<string> CreateLinkAsync(string code)
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { targetUrl = "https://example.com", code });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateLinkResponse>())!.Code;
    }

    private Task<long> ReadAccessCountAsync(string code) =>
        factory.QueryDatabaseAsync(db =>
            db.Links.Where(link => link.Code == code).Select(link => link.AccessCount).SingleAsync());
}
