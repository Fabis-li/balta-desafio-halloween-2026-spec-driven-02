namespace LinkShortener.Api.Application;

public abstract record ResolveLinkResult
{
    public sealed record Found(string TargetUrl) : ResolveLinkResult;

    public sealed record NotFound : ResolveLinkResult
    {
        public const string Error = "O link informado não existe.";
    }

    public sealed record Expired : ResolveLinkResult
    {
        public const string Error = "O link informado expirou.";
    }
}
