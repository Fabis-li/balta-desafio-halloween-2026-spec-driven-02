using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class CodeValidatorTests
{
    [Theory]
    [InlineData("PROM")]
    [InlineData("PROMO")]
    [InlineData("BLACKFRIDAY2")]
    [InlineData("ABCDEFGHIJKLMNOPQRS1")]
    [InlineData("123456789")]
    public void IsValid_AcceptsUppercaseLettersAndDigitsOneToNineWithFourToTwentyCharacters(string code)
    {
        Assert.True(CodeValidator.IsValid(code, out var error));
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("ABCDEFGHIJKLMNOPQRS12")]
    public void IsValid_RejectsCodesOutsideFourToTwentyCharacters(string code)
    {
        Assert.False(CodeValidator.IsValid(code, out var error));
        Assert.Equal(CodeValidator.LengthError, error);
    }

    [Theory]
    [InlineData("PROMO0")]
    [InlineData("promo")]
    [InlineData("Promo")]
    [InlineData("PRO MO")]
    [InlineData(" PROMO")]
    [InlineData("PROMO ")]
    [InlineData("PROMO\t")]
    [InlineData("PRO-MO")]
    [InlineData("PRO_MO")]
    [InlineData("PROMO!")]
    [InlineData("PROMÓ")]
    [InlineData("ÇAMPO")]
    [InlineData("ＰＲＯＭＯ")]
    [InlineData("PROMO٣")]
    public void IsValid_RejectsZeroLowercaseSpacesAndSpecialCharacters(string code)
    {
        Assert.False(CodeValidator.IsValid(code, out var error));
        Assert.Equal(CodeValidator.CharactersError, error);
    }
}
