namespace LinkShortener.Api.Application;

public interface ILinkRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken);

    Task<RedirectTarget?> FindRedirectTargetAsync(string code, CancellationToken cancellationToken);

    // D-02: incremento feito pelo banco, sem ler e regravar o valor
    Task IncrementAccessCountAsync(string code, CancellationToken cancellationToken);

    // Retorna false quando o código já foi gravado por outra requisição (D-04)
    Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken);
}
