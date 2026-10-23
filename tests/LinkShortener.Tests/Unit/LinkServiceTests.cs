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

        var result = await service.CreateAsync(ValidUrl, CancellationToken.None);

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

        var result = await service.CreateAsync(url, CancellationToken.None);

        var invalid = Assert.IsType<CreateLinkResult.Invalid>(result);
        Assert.NotEmpty(invalid.Error);
        Assert.Empty(_repository.Links);
    }

    [Fact]
    public async Task CreateAsync_WhenGeneratedCodeExists_TriesAnotherCode()
    {
        _repository.Links.Add(new Link { Code = "AAAAAAA" });
        var service = CreateService("AAAAAAA", "BBBBBBB");

        var created = Assert.IsType<CreateLinkResult.Created>(
            await service.CreateAsync(ValidUrl, CancellationToken.None));

        Assert.Equal("BBBBBBB", created.Link.Code);
    }

    [Fact]
    public async Task CreateAsync_WhenCodeIsTakenConcurrently_TriesAnotherCode()
    {
        _repository.CodesTakenOnInsert.Add("AAAAAAA");
        var service = CreateService("AAAAAAA", "BBBBBBB");

        var created = Assert.IsType<CreateLinkResult.Created>(
            await service.CreateAsync(ValidUrl, CancellationToken.None));

        Assert.Equal("BBBBBBB", created.Link.Code);
    }

    [Fact]
    public async Task CreateAsync_WhenEveryAttemptCollides_Throws()
    {
        _repository.Links.Add(new Link { Code = "AAAAAAA" });
        var service = CreateService("AAAAAAA");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(ValidUrl, CancellationToken.None));
    }

    private LinkService CreateService(params string[] codes) =>
        new(_repository, new SequenceCodeGenerator(codes), new FixedTimeProvider(Now));

    private sealed class SequenceCodeGenerator(string[] codes) : ICodeGenerator
    {
        private int _next;

        // Repete o último código quando a sequência acaba
        public string Generate() => codes[Math.Min(_next++, codes.Length - 1)];
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
