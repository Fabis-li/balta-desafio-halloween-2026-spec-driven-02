using LinkShortener.Api.Application;
using Microsoft.Extensions.Caching.Memory;

namespace LinkShortener.Api.Data;

// D-06: guarda em memória só destino e expiração; contador e estatísticas sempre vão ao banco
public class CachedLinkRepository(ILinkRepository inner, IMemoryCache cache, TimeProvider timeProvider) : ILinkRepository
{
    public static readonly TimeSpan MaxEntryLifetime = TimeSpan.FromMinutes(10);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        inner.CodeExistsAsync(code, cancellationToken);

    public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        inner.FindByCodeAsync(code, cancellationToken);

    public async Task<RedirectTarget?> FindRedirectTargetAsync(string code, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey(code), out RedirectTarget? cached))
            return cached;

        var target = await inner.FindRedirectTargetAsync(code, cancellationToken);

        // Código inexistente não entra no cache, para não esconder um link criado logo depois
        if (target is not null && EntryLifetime(target, timeProvider.GetUtcNow().UtcDateTime) is { } lifetime)
            cache.Set(CacheKey(code), target, lifetime);

        return target;
    }

    public Task IncrementAccessCountAsync(string code, CancellationToken cancellationToken) =>
        inner.IncrementAccessCountAsync(code, cancellationToken);

    public Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken) =>
        inner.TryAddAsync(link, cancellationToken);

    // RN-03: a entrada nunca vive além de ExpiresAtUtc; nulo quando o link já expirou e não deve entrar
    public static TimeSpan? EntryLifetime(RedirectTarget target, DateTime nowUtc)
    {
        if (target.ExpiresAtUtc is null)
            return MaxEntryLifetime;

        var remaining = target.ExpiresAtUtc.Value - nowUtc;

        if (remaining <= TimeSpan.Zero)
            return null;

        return remaining < MaxEntryLifetime ? remaining : MaxEntryLifetime;
    }

    private static string CacheKey(string code) => $"link-target:{code}";
}
