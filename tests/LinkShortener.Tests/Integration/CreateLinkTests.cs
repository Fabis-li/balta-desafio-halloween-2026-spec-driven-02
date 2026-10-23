using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Application;
using LinkShortener.Api.Endpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Tests.Integration;

public class CreateLinkTests(LinkShortenerFactory factory) : IClassFixture<LinkShortenerFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_WithValidUrl_CreatesLinkAndReturns201()
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { targetUrl = "https://example.com/pagina" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
        Assert.NotNull(body);
        Assert.Matches("^[A-Z1-9]{7}$", body.Code);
        Assert.Equal($"http://localhost/{body.Code}", body.ShortUrl);
        Assert.Equal(new Uri(body.ShortUrl), response.Headers.Location);

        var stored = await factory.QueryDatabaseAsync(db => db.Links.SingleAsync(link => link.Code == body.Code));
        Assert.Equal("https://example.com/pagina", stored.TargetUrl);
        Assert.Equal(0, stored.AccessCount);
    }

    [Fact]
    public async Task Post_TwiceWithSameUrl_CreatesTwoLinksWithDifferentCodes()
    {
        var first = await PostAndReadAsync("https://example.com/repetida");
        var second = await PostAndReadAsync("https://example.com/repetida");

        Assert.NotEqual(first.Code, second.Code);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("http://localhost")]
    [InlineData("não é uma url")]
    public async Task Post_WithInvalidUrl_Returns400WithProblemDetails(string url)
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { targetUrl = url });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("targetUrl", problem.Errors.Keys);
    }

    [Fact]
    public async Task Post_WithoutTargetUrl_Returns400WithProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal([TargetUrlValidator.RequiredError], problem.Errors["targetUrl"]);
    }

    [Fact]
    public async Task Post_WithCustomCode_CreatesLinkWithThatCode()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/links", new { targetUrl = "https://example.com/promo", code = "PROMOCAO" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
        Assert.NotNull(body);
        Assert.Equal("PROMOCAO", body.Code);
        Assert.Equal("http://localhost/PROMOCAO", body.ShortUrl);

        var stored = await factory.QueryDatabaseAsync(db => db.Links.SingleAsync(link => link.Code == "PROMOCAO"));
        Assert.Equal("https://example.com/promo", stored.TargetUrl);
    }

    [Fact]
    public async Task Post_WithCustomCodeInUse_Returns409WithProblemDetails()
    {
        await PostAndReadAsync("https://example.com/primeiro", "DUPLICADO");

        var response = await _client.PostAsJsonAsync(
            "/api/links", new { targetUrl = "https://example.com/segundo", code = "DUPLICADO" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(CreateLinkResult.CodeTaken.Error, problem.Detail);

        var stored = await factory.QueryDatabaseAsync(db => db.Links.SingleAsync(link => link.Code == "DUPLICADO"));
        Assert.Equal("https://example.com/primeiro", stored.TargetUrl);
    }

    [Theory]
    [InlineData("ABC", CodeValidator.LengthError)]
    [InlineData("promo", CodeValidator.CharactersError)]
    [InlineData("PROMO0", CodeValidator.CharactersError)]
    [InlineData("PRO MO", CodeValidator.CharactersError)]
    public async Task Post_WithInvalidCustomCode_Returns400WithProblemDetails(string code, string expectedError)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/links", new { targetUrl = "https://example.com", code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal([expectedError], problem.Errors["code"]);
    }

    private async Task<CreateLinkResponse> PostAndReadAsync(string url, string? code = null)
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { targetUrl = url, code });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateLinkResponse>())!;
    }
}
