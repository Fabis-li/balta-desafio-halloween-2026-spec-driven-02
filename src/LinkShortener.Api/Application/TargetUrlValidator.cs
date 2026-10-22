using System.Net;
using System.Net.Sockets;

namespace LinkShortener.Api.Application;

public static class TargetUrlValidator
{
    public const string RequiredError = "A URL de destino é obrigatória.";
    public const string TooLongError = "A URL de destino deve ter no máximo 2048 caracteres.";
    public const string InvalidError = "A URL de destino é inválida.";
    public const string SchemeError = "A URL de destino deve usar http ou https.";
    public const string NotPublicError = "A URL de destino precisa ser publicamente acessível.";

    private static readonly string[] ReservedHostSuffixes = [".localhost", ".local", ".internal", ".localdomain"];

    public static bool IsValid(string? targetUrl, out string error)
    {
        error = Validate(targetUrl) ?? string.Empty;
        return error.Length == 0;
    }

    private static string? Validate(string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
            return RequiredError;

        if (targetUrl.Length > Link.TargetUrlMaxLength)
            return TooLongError;

        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
            return InvalidError;

        // RN-02: só http e https
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return SchemeError;

        if (string.IsNullOrEmpty(uri.IdnHost))
            return InvalidError;

        // RN-02: verificação pelo próprio endereço, sem resolver DNS
        return IsPublicHost(uri) ? null : NotPublicError;
    }

    private static bool IsPublicHost(Uri uri)
    {
        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
            return IsPublicAddress(IPAddress.Parse(uri.IdnHost));

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();

        // Nome sem domínio, como "intranet", só resolve em rede local
        if (!host.Contains('.'))
            return false;

        return !ReservedHostSuffixes.Any(host.EndsWith);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.Equals(IPAddress.IPv6None)
                && !address.IsIPv6LinkLocal
                && !address.IsIPv6SiteLocal
                && !address.IsIPv6UniqueLocal
                && !address.IsIPv6Multicast;
        }

        var bytes = address.GetAddressBytes();

        return bytes[0] switch
        {
            0 => false,                                     // 0.0.0.0/8
            10 => false,                                    // 10.0.0.0/8
            100 when bytes[1] is >= 64 and <= 127 => false, // 100.64.0.0/10
            169 when bytes[1] == 254 => false,              // 169.254.0.0/16
            172 when bytes[1] is >= 16 and <= 31 => false,  // 172.16.0.0/12
            192 when bytes[1] == 168 => false,              // 192.168.0.0/16
            >= 224 => false,                                // multicast e reservados
            _ => true
        };
    }
}
