namespace LinkShortener.Api.Application;

public class Link
{
    public const int CodeMaxLength = 20;
    public const int TargetUrlMaxLength = 2048;

    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public long AccessCount { get; set; }
}
