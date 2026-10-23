namespace LinkShortener.Api.Application;

// D-06: só o que o redirect precisa, sem o contador, para poder ficar em cache
public record RedirectTarget(string TargetUrl, DateTime? ExpiresAtUtc);
