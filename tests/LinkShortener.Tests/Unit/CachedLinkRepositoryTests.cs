using LinkShortener.Api.Application;
using LinkShortener.Api.Data;
using Microsoft.Extensions.Caching.Memory;

namespace LinkShortener.Tests.Unit;

public class CachedLinkRepositoryTests : IDisposable
{
    private const string TargetUrl = "https://example.com/destino";

    private static readonly DateTime NowUtc = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly CountingLinkRepository _inner = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(NowUtc));
    private readonly CachedLinkRepository _repository;

    public CachedLinkRepositoryTests() => _repository = new CachedLinkRepository(_inner, _cache, _clock);

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task FindRedirectTargetAsync_SecondCallForSameCode_DoesNotReachTheDatabase()
    {
        _inner.Links.Add(new Link { Code = "PROMO", TargetUrl = TargetUrl });

        var first = await _repository.FindRedirectTargetAsync("PROMO", CancellationToken.None);
        var second = await _repository.FindRedirectTargetAsync("PROMO", CancellationToken.None);

        Assert.Equal(TargetUrl, first!.TargetUrl);
        Assert.Equal(first, second);
        Assert.Equal(1, _inner.RedirectTargetReads);
    }

    [Fact]
    public async Task FindRedirectTargetAsync_UnknownCode_IsNotCached()
    {
        Assert.Null(await _repository.FindRedirectTargetAsync("NOVO", CancellationToken.None));

        _inner.Links.Add(new Link { Code = "NOVO", TargetUrl = TargetUrl });

        Assert.Equal(TargetUrl, (await _repository.FindRedirectTargetAsync("NOVO", CancellationToken.None))!.TargetUrl);
        Assert.Equal(2, _inner.RedirectTargetReads);
    }

    [Fact]
    public async Task FindRedirectTargetAsync_AlreadyExpiredLink_IsNotCached()
    {
        _inner.Links.Add(new Link { Code = "VELHO", TargetUrl = TargetUrl, ExpiresAtUtc = NowUtc });

        await _repository.FindRedirectTargetAsync("VELHO", CancellationToken.None);
        await _repository.FindRedirectTargetAsync("VELHO", CancellationToken.None);

        Assert.Equal(2, _inner.RedirectTargetReads);
    }

    [Fact]
    public async Task IncrementAccessCountAsync_AlwaysReachesTheDatabase()
    {
        _inner.Links.Add(new Link { Code = "PROMO", TargetUrl = TargetUrl });

        for (var i = 0; i < 3; i++)
        {
            await _repository.FindRedirectTargetAsync("PROMO", CancellationToken.None);
            await _repository.IncrementAccessCountAsync("PROMO", CancellationToken.None);
        }

        Assert.Equal(3, _inner.Links.Single().AccessCount);
    }

    [Fact]
    public async Task FindByCodeAsync_UsedByStats_IsNeverCached()
    {
        _inner.Links.Add(new Link { Code = "PROMO", TargetUrl = TargetUrl });

        await _repository.FindByCodeAsync("PROMO", CancellationToken.None);
        _inner.Links.Single().AccessCount = 5;
        var link = await _repository.FindByCodeAsync("PROMO", CancellationToken.None);

        Assert.Equal(5, link!.AccessCount);
    }

    [Fact]
    public void EntryLifetime_WithoutExpiration_IsTheMaximum()
    {
        var lifetime = CachedLinkRepository.EntryLifetime(new RedirectTarget(TargetUrl, null), NowUtc);

        Assert.Equal(CachedLinkRepository.MaxEntryLifetime, lifetime);
    }

    [Fact]
    public void EntryLifetime_WithFarExpiration_IsTheMaximum()
    {
        var lifetime = CachedLinkRepository.EntryLifetime(new RedirectTarget(TargetUrl, NowUtc.AddDays(1)), NowUtc);

        Assert.Equal(CachedLinkRepository.MaxEntryLifetime, lifetime);
    }

    [Fact]
    public void EntryLifetime_WithNearExpiration_EndsAtTheExpiration()
    {
        var lifetime = CachedLinkRepository.EntryLifetime(new RedirectTarget(TargetUrl, NowUtc.AddSeconds(30)), NowUtc);

        Assert.Equal(TimeSpan.FromSeconds(30), lifetime);
    }

    [Fact]
    public void EntryLifetime_WithPastExpiration_IsNull()
    {
        Assert.Null(CachedLinkRepository.EntryLifetime(new RedirectTarget(TargetUrl, NowUtc), NowUtc));
        Assert.Null(CachedLinkRepository.EntryLifetime(new RedirectTarget(TargetUrl, NowUtc.AddSeconds(-1)), NowUtc));
    }

    [Fact]
    public async Task ResolveAsync_LinkThatExpiresWhileCached_ReturnsExpiredWithoutCounting()
    {
        _inner.Links.Add(new Link { Code = "PROMO", TargetUrl = TargetUrl, ExpiresAtUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) });
        var service = new LinkService(_repository, new RandomCodeGenerator(), _clock);

        _clock.Now = new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero);
        Assert.IsType<ResolveLinkResult.Found>(await service.ResolveAsync("PROMO", CancellationToken.None));

        // O relógio do cache é o real, então a entrada ainda está lá quando o link vence
        _clock.Now = new DateTimeOffset(2026, 11, 1, 0, 0, 1, TimeSpan.Zero);
        Assert.IsType<ResolveLinkResult.Expired>(await service.ResolveAsync("PROMO", CancellationToken.None));

        Assert.Equal(1, _inner.RedirectTargetReads);
        Assert.Equal(1, _inner.Links.Single().AccessCount);
    }

    private sealed class CountingLinkRepository : ILinkRepository
    {
        public List<Link> Links { get; } = [];

        public int RedirectTargetReads { get; private set; }

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(Links.Any(link => link.Code == code));

        public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(Links.SingleOrDefault(link => link.Code == code));

        public Task<RedirectTarget?> FindRedirectTargetAsync(string code, CancellationToken cancellationToken)
        {
            RedirectTargetReads++;

            return Task.FromResult(Links
                .Where(link => link.Code == code)
                .Select(link => new RedirectTarget(link.TargetUrl, link.ExpiresAtUtc))
                .SingleOrDefault());
        }

        public Task IncrementAccessCountAsync(string code, CancellationToken cancellationToken)
        {
            foreach (var link in Links.Where(link => link.Code == code))
                link.AccessCount++;

            return Task.CompletedTask;
        }

        public Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
        {
            Links.Add(link);
            return Task.FromResult(true);
        }
    }
}
