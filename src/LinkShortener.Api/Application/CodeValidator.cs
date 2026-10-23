namespace LinkShortener.Api.Application;

public static class CodeValidator
{
    public const int MinLength = 4;
    public const int MaxLength = Link.CodeMaxLength;

    // RN-05: A-Z maiúsculo e 1-9, tanto para código automático quanto personalizado
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";

    public const string LengthError = "O código deve ter de 4 a 20 caracteres.";
    public const string CharactersError =
        "O código deve usar apenas letras maiúsculas de A a Z e números de 1 a 9, sem espaços.";

    // RF-03: código próprio informado pelo criador
    public static bool IsValid(string code, out string error)
    {
        error = Validate(code) ?? string.Empty;
        return error.Length == 0;
    }

    private static string? Validate(string code)
    {
        if (code.Length is < MinLength or > MaxLength)
            return LengthError;

        return code.All(Alphabet.Contains) ? null : CharactersError;
    }
}
