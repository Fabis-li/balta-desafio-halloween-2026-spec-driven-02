using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace LinkShortener.Api.Application;

public static class ManagementKey
{
    private const int SizeInBytes = 32;

    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SizeInBytes));

    // D-05: a chave tem 256 bits aleatórios, então SHA-256 sem salt basta
    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
