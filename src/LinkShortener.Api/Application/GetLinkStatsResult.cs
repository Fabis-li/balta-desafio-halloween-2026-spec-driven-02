namespace LinkShortener.Api.Application;

public abstract record GetLinkStatsResult
{
    public sealed record Found(string Code, long AccessCount) : GetLinkStatsResult;

    public sealed record NotFound : GetLinkStatsResult
    {
        public const string Error = ResolveLinkResult.NotFound.Error;
    }
}
