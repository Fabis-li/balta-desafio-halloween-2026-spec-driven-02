using System.Globalization;

namespace LinkShortener.Api.Application;

public static class ExpirationDateParser
{
    public const string Format = "yyyy-MM-dd";

    public const string FormatError = "A data de expiração deve estar no formato AAAA-MM-DD.";
    public const string PastError = "A data de expiração não pode estar no passado.";

    // RF-06, D-09: a data vale até o fim do dia em UTC, então o limite é 00:00 UTC do dia seguinte
    public static bool TryParse(string expiresOn, DateTime nowUtc, out DateTime expiresAtUtc, out string error)
    {
        expiresAtUtc = default;
        error = string.Empty;

        if (!DateOnly.TryParseExact(expiresOn, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            error = FormatError;
            return false;
        }

        if (date < DateOnly.FromDateTime(nowUtc))
        {
            error = PastError;
            return false;
        }

        expiresAtUtc = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return true;
    }
}
