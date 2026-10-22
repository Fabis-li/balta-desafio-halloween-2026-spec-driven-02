using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class ManagementKeyTests
{
    [Fact]
    public void Generate_ReturnsUrlSafeKeysThatDoNotRepeat()
    {
        var keys = Enumerable.Range(0, 1_000).Select(_ => ManagementKey.Generate()).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, key => Assert.Matches("^[A-Za-z0-9_-]{43}$", key));
    }

    [Fact]
    public void Hash_IsDeterministicAndDifferentFromTheKey()
    {
        var key = ManagementKey.Generate();

        Assert.Equal(ManagementKey.Hash(key), ManagementKey.Hash(key));
        Assert.NotEqual(key, ManagementKey.Hash(key));
        Assert.NotEqual(ManagementKey.Hash(key), ManagementKey.Hash(ManagementKey.Generate()));
    }
}
