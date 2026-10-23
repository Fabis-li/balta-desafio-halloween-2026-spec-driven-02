using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class LinkServiceTests
{
    private const string ValidUrl = "https://example.com/pagina";

    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeLinkRepository _repository = new();

    [Fact]
    public async Task CreateAsync_WithValidUrl_StoresLinkWithGeneratedCode()
    {
        var service = CreateService("ABC1234");

        var result = await service.CreateAsync(new(ValidUrl), CancellationToken.None);

        var created = Assert.IsType<CreateLinkResult.Created>(result);
        var stored = Assert.Single(_repository.Links);
        Assert.Same(stored, created.Link);
        Assert.Equal("ABC1234", stored.Code);
        Assert.Equal(ValidUrl, stored.TargetUrl);
        Assert.Equal(Now.UtcDateTime, stored.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedAtUtc.Kind);
        Assert.Null(stored.ExpiresAtUtc);
        Assert.Equal(0, stored.AccessCount);
        Assert.NotEqual(Guid.Empty, stored.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ftp://example.com")]
    [InlineData("http://localhost")]
    public async Task CreateAsync_WithInvalidUrl_ReturnsInvalidAndStoresNothing(string? url)
    {
        var service = CreateService("ABC1234");

        var result = await service.CreateAsync(new(url), CancellationToken.None);

        var invalid = Assert.IsType<CreateLinkResult.Invalid>(result);
        Assert.Equal(LinkService.TargetUrlField, invalid.Field);
        Assert.NotEmpty(invalid.Error);
        Assert.Empty(_repository.Links);
    }

    [Fact]
    public async Task CreateAsync_WithCustomCode_StoresLinkWithThatCodeWithoutGenerating()
    {
        var generator = new SequenceCodeGenerator(["ABC1234"]);
        var service = new LinkService(_repository, generator, new FixedTimeProvider(Now));

        var created = Assert.IsType<CreateLinkResult.Created>(
            await service.CreateAsync(new(ValidUrl, "PROMO"), CancellationToken.None));

        Assert.Equal("PROMO", created.Link.Code);
        Assert.Same(created.Link, Assert.Single(_repository.Links));
        Assert.Equal(0, generator.Calls);
    }

    [Theory]
    [InlineData("ABC", CodeValidator.LengthError)]
    [InlineData("promo", CodeValidator.CharactersError)]
    [InlineData("PROMO2026", CodeValidator.CharactersError)]
    public async Task CreateAsync_WithInvalidCustomCode_ReturnsInvalidAndStoresNothing(string code, string expectedError)
    {
        var service = CreateService("ABC1234");

        var result = await service.CreateAsync(new(ValidUrl, code), CancellationToken.None);

        var invalid = Assert.IsType<CreateLinkResult.Invalid>(result);
        Assert.Equal(LinkService.CodeField, invalid.Field);
        Assert.Equal(expectedError, invalid.Error);
        Assert.Empty(_repository.Links);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidUrlAndCustomCode_ReportsTheUrlFirst()
    {
        var service = CreateService("ABC1234");

        var invalid = Assert.IsType<CreateLinkResult.Invalid>(
            await service.CreateAsync(new("ftp://example.com", "promo"), CancellationToken.None));

        Assert.Equal(LinkService.TargetUrlField, invalid.Field);
    }

    [Fact]
    public async Task CreateAsync_WithCustomCodeInUse_ReturnsCodeTakenWithoutRetrying()
    {
        _repository.Links.Add(new Link { Code = "PROMO" });
        var service = CreateService("ABC1234");

        var result = await service.CreateAsync(new(ValidUrl, "PROMO"), CancellationToken.None);

        Assert.IsType<CreateLinkResult.CodeTaken>(result);
        Assert.Single(_repository.Links);
    }

    [Fact]
    public async Task CreateAsync_WhenCustomCodeIsTakenConcurrently_ReturnsCodeTaken()
    {
        _repository.CodesTakenOnInsert.Add("PROMO");
        var service = CreateService("ABC1234");

        var result = await service.CreateAsync(new(ValidUrl, "PROMO"), CancellationToken.None);

        Assert.IsType<CreateLinkResult.CodeTaken>(result);
        Assert.DoesNotContain(_repository.Links, link => link.TargetUrl == ValidUrl);
    }

    [Fact]
    public async Task CreateAsync_WhenGeneratedCodeExists_TriesAnotherCode()
    {
        _repository.Links.Add(new Link { Code = "AAAAAAA" });
        var service = CreateService("AAAAAAA", "BBBBBBB");

        var created = Assert.IsType<CreateLinkResult.Created>(
            await service.CreateAsync(new(ValidUrl), CancellationToken.None));

        Assert.Equal("BBBBBBB", created.Link.Code);
    }

    [Fact]
    public async Task CreateAsync_WhenCodeIsTakenConcurrently_TriesAnotherCode()
    {
        _repository.CodesTakenOnInsert.Add("AAAAAAA");
        var service = CreateService("AAAAAAA", "BBBBBBB");

        var created = Assert.IsType<CreateLinkResult.Created>(
            await service.CreateAsync(new(ValidUrl), CancellationToken.None));

        Assert.Equal("BBBBBBB", created.Link.Code);
    }

    [Fact]
    public async Task CreateAsync_WhenEveryAttemptCollides_Throws()
    {
        _repository.Links.Add(new Link { Code = "AAAAAAA" });
        var service = CreateService("AAAAAAA");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(new(ValidUrl), CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_WithExistingCode_ReturnsTargetUrl()
    {
        _repository.Links.Add(new Link { Code = "PROMO", TargetUrl = ValidUrl });
        var service = CreateService("ABC1234");

        var found = Assert.IsType<ResolveLinkResult.Found>(
            await service.ResolveAsync("PROMO", CancellationToken.None));

        Assert.Equal(ValidUrl, found.TargetUrl);
    }

    [Theory]
    [InlineData("NAOEXISTE")]
    [InlineData("promo")]
    public async Task ResolveAsync_WithUnknownCode_ReturnsNotFound(string code)
    {
        _repository.Links.Add(new Link { Code = "PROMO", TargetUrl = ValidUrl });
        var service = CreateService("ABC1234");

        var result = await service.ResolveAsync(code, CancellationToken.None);

        Assert.IsType<ResolveLinkResult.NotFound>(result);
        Assert.Equal(0, _repository.Links.Single().AccessCount);
    }

    [Fact]
    public async Task ResolveAsync_CountsEveryAccessIncludingRepeatedOnes()
    {
        var link = new Link { Code = "PROMO", TargetUrl = ValidUrl };
        _repository.Links.Add(link);
        var service = CreateService("ABC1234");

        for (var i = 0; i < 3; i++)
            await service.ResolveAsync("PROMO", CancellationToken.None);

        Assert.Equal(3, link.AccessCount);
    }

    [Fact]
    public async Task GetStatsAsync_WithExistingCode_ReturnsAccessCountWithoutCountingAnAccess()
    {
        _repository.Links.Add(new Link { Code = "PROMO", TargetUrl = ValidUrl, AccessCount = 42 });
        var service = CreateService("ABC1234");

        var found = Assert.IsType<GetLinkStatsResult.Found>(
            await service.GetStatsAsync("PROMO", CancellationToken.None));

        Assert.Equal("PROMO", found.Code);
        Assert.Equal(42, found.AccessCount);
        Assert.Equal(42, _repository.Links.Single().AccessCount);
    }

    [Fact]
    public async Task GetStatsAsync_WithUnknownCode_ReturnsNotFound()
    {
        var service = CreateService("ABC1234");

        var result = await service.GetStatsAsync("NAOEXISTE", CancellationToken.None);

        Assert.IsType<GetLinkStatsResult.NotFound>(result);
    }

    private LinkService CreateService(params string[] codes) =>
        new(_repository, new SequenceCodeGenerator(codes), new FixedTimeProvider(Now));

    private sealed class SequenceCodeGenerator(string[] codes) : ICodeGenerator
    {
        public int Calls { get; private set; }

        // Repete o último código quando a sequência acaba
        public string Generate() => codes[Math.Min(Calls++, codes.Length - 1)];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeLinkRepository : ILinkRepository
    {
        public List<Link> Links { get; } = [];

        // Simula outra requisição gravando o mesmo código entre a checagem e o insert
        public HashSet<string> CodesTakenOnInsert { get; } = [];

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(Links.Any(link => link.Code == code));

        public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(Links.SingleOrDefault(link => link.Code == code));

        public Task IncrementAccessCountAsync(string code, CancellationToken cancellationToken)
        {
            foreach (var link in Links.Where(link => link.Code == code))
                link.AccessCount++;

            return Task.CompletedTask;
        }

        public Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
        {
            if (CodesTakenOnInsert.Remove(link.Code))
            {
                Links.Add(new Link { Code = link.Code });
                return Task.FromResult(false);
            }

            Links.Add(link);
            return Task.FromResult(true);
        }
    }
}
