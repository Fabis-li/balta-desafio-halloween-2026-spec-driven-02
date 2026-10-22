using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class TargetUrlValidatorTests
{
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    [InlineData("https://www.example.com/caminho/longo?busca=1&outro=2#ancora")]
    [InlineData("https://example.com:8443/api")]
    [InlineData("http://8.8.8.8")]
    [InlineData("https://[2001:4860:4860::8888]/")]
    [InlineData("https://exemplo.com.br/ação")]
    public void IsValid_AcceptsPublicHttpAndHttpsUrls(string url)
    {
        Assert.True(TargetUrlValidator.IsValid(url, out var error));
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_RejectsMissingUrl(string? url)
    {
        Assert.False(TargetUrlValidator.IsValid(url, out var error));
        Assert.Equal(TargetUrlValidator.RequiredError, error);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("/caminho/relativo")]
    [InlineData("não é uma url")]
    [InlineData("http://")]
    public void IsValid_RejectsMalformedUrls(string url)
    {
        Assert.False(TargetUrlValidator.IsValid(url, out var error));
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("ftp://example.com/arquivo.txt")]
    [InlineData("file:///C:/Windows/system.ini")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:alguem@example.com")]
    [InlineData("data:text/html,<h1>oi</h1>")]
    public void IsValid_RejectsSchemesOtherThanHttpAndHttps(string url)
    {
        Assert.False(TargetUrlValidator.IsValid(url, out var error));
        Assert.Equal(TargetUrlValidator.SchemeError, error);
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://LOCALHOST:5000")]
    [InlineData("http://localhost.")]
    [InlineData("http://app.localhost")]
    [InlineData("http://impressora.local")]
    [InlineData("http://servico.internal")]
    [InlineData("http://intranet")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.10.20.30")]
    [InlineData("http://2130706433")]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://100.64.0.1")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.31.255.255")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://224.0.0.1")]
    [InlineData("http://255.255.255.255")]
    [InlineData("http://[::1]")]
    [InlineData("http://[::]")]
    [InlineData("http://[fe80::1]")]
    [InlineData("http://[fc00::1]")]
    [InlineData("http://[fd12:3456::1]")]
    [InlineData("http://[ff02::1]")]
    [InlineData("http://[::ffff:127.0.0.1]")]
    [InlineData("http://[::ffff:192.168.0.1]")]
    public void IsValid_RejectsUrlsThatAreNotPubliclyAccessible(string url)
    {
        Assert.False(TargetUrlValidator.IsValid(url, out var error));
        Assert.Equal(TargetUrlValidator.NotPublicError, error);
    }

    [Theory]
    [InlineData("http://172.15.0.1")]
    [InlineData("http://172.32.0.1")]
    [InlineData("http://100.63.255.255")]
    [InlineData("http://100.128.0.1")]
    public void IsValid_AcceptsPublicAddressesNextToPrivateRanges(string url)
    {
        Assert.True(TargetUrlValidator.IsValid(url, out _));
    }

    [Fact]
    public void IsValid_AcceptsUrlWithExactly2048Characters()
    {
        var url = BuildUrlWithLength(2048);

        Assert.True(TargetUrlValidator.IsValid(url, out _));
    }

    [Fact]
    public void IsValid_RejectsUrlLongerThan2048Characters()
    {
        var url = BuildUrlWithLength(2049);

        Assert.False(TargetUrlValidator.IsValid(url, out var error));
        Assert.Equal(TargetUrlValidator.TooLongError, error);
    }

    private static string BuildUrlWithLength(int length)
    {
        const string prefix = "https://example.com/";
        return prefix + new string('a', length - prefix.Length);
    }
}
