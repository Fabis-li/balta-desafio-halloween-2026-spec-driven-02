namespace LinkShortener.Api.Application;

public abstract record CreateLinkResult
{
    public sealed record Created(Link Link) : CreateLinkResult;

    public sealed record Invalid(string Error) : CreateLinkResult;
}
