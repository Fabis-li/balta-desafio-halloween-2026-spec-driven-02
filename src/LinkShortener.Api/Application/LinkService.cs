namespace LinkShortener.Api.Application;

public class LinkService(ILinkRepository repository, ICodeGenerator codeGenerator, TimeProvider timeProvider)
{
    public const int MaxCodeAttempts = 10;

    public const string TargetUrlField = "targetUrl";
    public const string CodeField = "code";

    public async Task<CreateLinkResult> CreateAsync(CreateLinkCommand command, CancellationToken cancellationToken)
    {
        if (!TargetUrlValidator.IsValid(command.TargetUrl, out var error))
            return new CreateLinkResult.Invalid(TargetUrlField, error);

        if (command.Code is not null)
            return await CreateWithCustomCodeAsync(command.TargetUrl!, command.Code, cancellationToken);

        // RN-06: código automático já em uso gera outra tentativa
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var code = codeGenerator.Generate();

            if (await repository.CodeExistsAsync(code, cancellationToken))
                continue;

            var link = NewLink(code, command.TargetUrl!);

            if (await repository.TryAddAsync(link, cancellationToken))
                return new CreateLinkResult.Created(link);
        }

        throw new InvalidOperationException(
            $"Não foi possível gerar um código único após {MaxCodeAttempts} tentativas.");
    }

    private async Task<CreateLinkResult> CreateWithCustomCodeAsync(
        string targetUrl, string code, CancellationToken cancellationToken)
    {
        if (!CodeValidator.IsValid(code, out var error))
            return new CreateLinkResult.Invalid(CodeField, error);

        // RN-01: código próprio em uso é recusado, sem nova tentativa
        if (await repository.CodeExistsAsync(code, cancellationToken))
            return new CreateLinkResult.CodeTaken();

        var link = NewLink(code, targetUrl);

        // D-04: o índice único pega quem gravou o mesmo código depois da checagem
        return await repository.TryAddAsync(link, cancellationToken)
            ? new CreateLinkResult.Created(link)
            : new CreateLinkResult.CodeTaken();
    }

    private Link NewLink(string code, string targetUrl) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        TargetUrl = targetUrl,
        CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
    };
}
