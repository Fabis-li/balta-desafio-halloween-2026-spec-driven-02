namespace LinkShortener.Api.Application;

public abstract record CreateLinkResult
{
    public sealed record Created(Link Link) : CreateLinkResult;

    // Field é o nome do campo da entrada que falhou na validação
    public sealed record Invalid(string Field, string Error) : CreateLinkResult;

    public sealed record CodeTaken : CreateLinkResult
    {
        public const string Error = "O código informado já está em uso.";
    }
}
