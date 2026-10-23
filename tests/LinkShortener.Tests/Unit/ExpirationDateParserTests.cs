using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class ExpirationDateParserTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("2026-10-31", 2026, 11, 1)]
    [InlineData("2026-11-01", 2026, 11, 2)]
    [InlineData("2026-12-31", 2027, 1, 1)]
    [InlineData("2028-02-29", 2028, 3, 1)]
    public void TryParse_WithTodayOrFutureDate_ReturnsStartOfNextDayInUtc(string expiresOn, int year, int month, int day)
    {
        Assert.True(ExpirationDateParser.TryParse(expiresOn, NowUtc, out var expiresAtUtc, out var error));

        Assert.Empty(error);
        Assert.Equal(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc), expiresAtUtc);
        Assert.Equal(DateTimeKind.Utc, expiresAtUtc.Kind);
    }

    [Fact]
    public void TryParse_UsesTheUtcDateNotTheLocalOne()
    {
        // 23h30 UTC do dia 31 ainda é dia 31, mesmo onde já é dia 1 no fuso local
        var lateNightUtc = new DateTime(2026, 10, 31, 23, 30, 0, DateTimeKind.Utc);

        Assert.True(ExpirationDateParser.TryParse("2026-10-31", lateNightUtc, out _, out _));
    }

    [Theory]
    [InlineData("2026-10-30")]
    [InlineData("2020-01-01")]
    public void TryParse_WithPastDate_ReturnsPastError(string expiresOn)
    {
        Assert.False(ExpirationDateParser.TryParse(expiresOn, NowUtc, out _, out var error));
        Assert.Equal(ExpirationDateParser.PastError, error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("31/10/2026")]
    [InlineData("2026-1-5")]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01")]
    [InlineData("2026-11-01T10:00:00Z")]
    [InlineData(" 2026-11-01")]
    [InlineData("amanhã")]
    public void TryParse_WithInvalidFormat_ReturnsFormatError(string expiresOn)
    {
        Assert.False(ExpirationDateParser.TryParse(expiresOn, NowUtc, out _, out var error));
        Assert.Equal(ExpirationDateParser.FormatError, error);
    }
}
