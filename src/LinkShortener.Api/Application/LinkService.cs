namespace LinkShortener.Api.Application;

public class LinkService(ILinkRepository repository, ICodeGenerator codeGenerator, TimeProvider timeProvider)
{
    public const int MaxCodeAttempts = 10;

    public const string TargetUrlField = "targetUrl";
    public const string CodeField = "code";
    public const string ExpiresOnField = "expiresOn";

    public async Task<CreateLinkResult> CreateAsync(CreateLinkCommand command, CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (!TargetUrlValidator.IsValid(command.TargetUrl, out var error))
            return new CreateLinkResult.Invalid(TargetUrlField, error);

        if (command.Code is not null && !CodeValidator.IsValid(command.Code, out error))
            return new CreateLinkResult.Invalid(CodeField, error);

        DateTime? expiresAtUtc = null;

        if (command.ExpiresOn is not null)
        {
            if (!ExpirationDateParser.TryParse(command.ExpiresOn, nowUtc, out var parsed, out error))
                return new CreateLinkResult.Invalid(ExpiresOnField, error);

            expiresAtUtc = parsed;
        }

        if (command.Code is not null)
            return await CreateWithCustomCodeAsync(NewLink(command.Code, command.TargetUrl!, nowUtc, expiresAtUtc), cancellationToken);

        // RN-06: código automático já em uso gera outra tentativa
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var code = codeGenerator.Generate();

            if (await repository.CodeExistsAsync(code, cancellationToken))
                continue;

            var link = NewLink(code, command.TargetUrl!, nowUtc, expiresAtUtc);

            if (await repository.TryAddAsync(link, cancellationToken))
                return new CreateLinkResult.Created(link);
        }

        throw new InvalidOperationException(
            $"Não foi possível gerar um código único após {MaxCodeAttempts} tentativas.");
    }

    // RF-04: acesso ao link curto leva ao destino
    public async Task<ResolveLinkResult> ResolveAsync(string code, CancellationToken cancellationToken)
    {
        var target = await repository.FindRedirectTargetAsync(code, cancellationToken);

        if (target is null)
            return new ResolveLinkResult.NotFound();

        // RN-03: link expirado não redireciona e não conta acesso, mesmo vindo do cache (D-06)
        if (IsExpired(target.ExpiresAtUtc))
            return new ResolveLinkResult.Expired();

        // RF-05, RN-04: todo acesso válido conta, inclusive repetido ou automatizado
        await repository.IncrementAccessCountAsync(code, cancellationToken);

        return new ResolveLinkResult.Found(target.TargetUrl);
    }

    // RF-07: qualquer pessoa que conheça o código consulta o total, sem contar como acesso
    public async Task<GetLinkStatsResult> GetStatsAsync(string code, CancellationToken cancellationToken)
    {
        var link = await repository.FindByCodeAsync(code, cancellationToken);

        return link is null
            ? new GetLinkStatsResult.NotFound()
            : new GetLinkStatsResult.Found(link.Code, link.AccessCount);
    }

    private async Task<CreateLinkResult> CreateWithCustomCodeAsync(Link link, CancellationToken cancellationToken)
    {
        // RN-01: código próprio em uso é recusado, sem nova tentativa
        if (await repository.CodeExistsAsync(link.Code, cancellationToken))
            return new CreateLinkResult.CodeTaken();

        // D-04: o índice único pega quem gravou o mesmo código depois da checagem
        return await repository.TryAddAsync(link, cancellationToken)
            ? new CreateLinkResult.Created(link)
            : new CreateLinkResult.CodeTaken();
    }

    // D-09: válido enquanto agora < ExpiresAtUtc
    private bool IsExpired(DateTime? expiresAtUtc) =>
        expiresAtUtc is not null && timeProvider.GetUtcNow().UtcDateTime >= expiresAtUtc.Value;

    private static Link NewLink(string code, string targetUrl, DateTime nowUtc, DateTime? expiresAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        TargetUrl = targetUrl,
        CreatedAtUtc = nowUtc,
        ExpiresAtUtc = expiresAtUtc
    };
}
