using System.Security.Cryptography;

namespace LinkShortener.Api.Application;

public class RandomCodeGenerator : ICodeGenerator
{
    public const int CodeLength = 7;

    // RF-02, RN-05, D-03: A-Z maiúsculo e 1-9; o 0 fica de fora para não ser confundido com O
    public const string Alphabet = CodeValidator.Alphabet;

    public string Generate() => RandomNumberGenerator.GetString(Alphabet, CodeLength);
}
