using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class RandomCodeGeneratorTests
{
    private const int Samples = 10_000;

    private readonly RandomCodeGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsCodeWithSevenCharacters()
    {
        for (var i = 0; i < Samples; i++)
            Assert.Equal(7, _generator.Generate().Length);
    }

    [Fact]
    public void Generate_UsesOnlyUppercaseLettersAndDigitsOneToNine()
    {
        for (var i = 0; i < Samples; i++)
            Assert.Matches("^[A-Z1-9]{7}$", _generator.Generate());
    }

    [Fact]
    public void Generate_NeverUsesZeroLowercaseOrSpecialCharacters()
    {
        for (var i = 0; i < Samples; i++)
        {
            var code = _generator.Generate();

            Assert.DoesNotContain('0', code);
            Assert.DoesNotContain(code, char.IsLower);
            Assert.DoesNotContain(code, char.IsWhiteSpace);
            Assert.All(code, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
        }
    }

    [Fact]
    public void Generate_CoversTheWholeAlphabet()
    {
        var seen = new HashSet<char>();

        for (var i = 0; i < Samples; i++)
            seen.UnionWith(_generator.Generate());

        Assert.Equal(RandomCodeGenerator.Alphabet.ToHashSet(), seen);
    }

    [Fact]
    public void Generate_ProducesDifferentCodesAcrossCalls()
    {
        var codes = Enumerable.Range(0, Samples).Select(_ => _generator.Generate()).ToHashSet();

        // 35^7 combinações tornam colisões em 10 mil amostras raríssimas
        Assert.True(codes.Count >= Samples - 1);
    }

    [Fact]
    public void Alphabet_MatchesSpecification()
    {
        Assert.Equal(35, RandomCodeGenerator.Alphabet.Length);
        Assert.Equal(RandomCodeGenerator.Alphabet.Length, RandomCodeGenerator.Alphabet.Distinct().Count());
        Assert.DoesNotContain('0', RandomCodeGenerator.Alphabet);
    }
}
