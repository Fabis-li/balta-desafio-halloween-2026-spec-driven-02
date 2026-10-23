using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Application;
using LinkShortener.Api.Endpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Tests.Integration;

// Cada teste cria a própria fábrica porque avança o relógio compartilhado da aplicação
public class ExpirationTests : IAsyncLifetime
{
    private readonly LinkShortenerFactory _factory = new();
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Get_LinkExpiringToday_WorksUntilEndOfDayUtcThenReturns410WithoutCounting()
    {
        var code = await CreateLinkAsync("HOJE", "2026-10-31");

        _factory.Clock.Now = new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.Found, (await _client.GetAsync($"/{code}")).StatusCode);

        _factory.Clock.Now = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var response = await _client.GetAsync($"/{code}");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(ResolveLinkResult.Expired.Error, problem.Detail);

        Assert.Equal(1, await ReadAccessCountAsync(code));
    }

    [Fact]
    public async Task Post_WithExpirationDate_StoresStartOfNextDayInUtc()
    {
        var code = await CreateLinkAsync("FUTURO", "2026-12-25");

        var stored = await _factory.QueryDatabaseAsync(db => db.Links.SingleAsync(link => link.Code == code));
        Assert.Equal(new DateTime(2026, 12, 26, 0, 0, 0), stored.ExpiresAtUtc);
    }

    [Fact]
    public async Task Get_LinkWithoutExpiration_KeepsWorkingInTheFuture()
    {
        var code = await CreateLinkAsync("ETERNO", null);

        _factory.Clock.Now = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(HttpStatusCode.Found, (await _client.GetAsync($"/{code}")).StatusCode);
    }

    [Theory]
    [InlineData("2026-10-30", ExpirationDateParser.PastError)]
    [InlineData("30/10/2026", ExpirationDateParser.FormatError)]
    public async Task Post_WithPastOrMalformedDate_Returns400WithProblemDetails(string expiresOn, string expectedError)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/links", new { targetUrl = "https://example.com", expiresOn });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal([expectedError], problem.Errors["expiresOn"]);
    }

    private async Task<string> CreateLinkAsync(string code, string? expiresOn)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/links", new { targetUrl = "https://example.com", code, expiresOn });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateLinkResponse>())!.Code;
    }

    private Task<long> ReadAccessCountAsync(string code) =>
        _factory.QueryDatabaseAsync(db =>
            db.Links.Where(link => link.Code == code).Select(link => link.AccessCount).SingleAsync());
}
