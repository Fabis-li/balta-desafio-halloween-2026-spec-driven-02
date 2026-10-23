namespace LinkShortener.Api.Application;

public class LinkService(ILinkRepository repository, ICodeGenerator codeGenerator, TimeProvider timeProvider)
{
    public const int MaxCodeAttempts = 10;

    public async Task<CreateLinkResult> CreateAsync(string? targetUrl, CancellationToken cancellationToken)
    {
        if (!TargetUrlValidator.IsValid(targetUrl, out var error))
            return new CreateLinkResult.Invalid(error);

        // RN-06: código automático já em uso gera outra tentativa
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var code = codeGenerator.Generate();

            if (await repository.CodeExistsAsync(code, cancellationToken))
                continue;

            var link = new Link
            {
                Id = Guid.NewGuid(),
                Code = code,
                TargetUrl = targetUrl!,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };

            if (await repository.TryAddAsync(link, cancellationToken))
                return new CreateLinkResult.Created(link);
        }

        throw new InvalidOperationException(
            $"Não foi possível gerar um código único após {MaxCodeAttempts} tentativas.");
    }
}
