using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Learner.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Learner.Infrastructure.External;

/// <summary>
/// Q4 credential link check (Figma §7): fetch a public page within 5 s and look for the learner's name. The handler
/// refuses private, loopback, link-local and multicast addresses on every connection (redirects included), so a
/// learner can't make the server call internal Azure endpoints (SSRF).
/// </summary>
internal sealed partial class PublicLinkChecker(HttpClient http, ILogger<PublicLinkChecker> logger) : ILinkChecker
{
    public const string ClientName = "link-checker";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const int MaxBodyBytes = 512 * 1024;

    public async Task<Lookup<bool>> PageMentionsAsync(Uri url, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
        {
            return Lookup<bool>.NotFound();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Lookup<bool>.NotFound();
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) && mediaType != "application/xhtml+xml")
            {
                return Lookup<bool>.Found(false);
            }

            var page = Normalize(StripTags(await ReadLimitedAsync(response.Content, timeout.Token)));
            return Lookup<bool>.Found(names.Any(n => n.Length > 0 && page.Contains(Normalize(n), StringComparison.Ordinal)));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Lookup<bool>.Timeout();
        }
        catch (Exception e) when (ExternalCall.Failed(e, cancellationToken))
        {
            // Includes BlockedAddressException from the connect callback. The URL is not logged (it may be personal).
            LogFailed(logger, e.GetType().Name);
            return Lookup<bool>.Unavailable();
        }
    }

    /// <summary>The handler for the named client: no cookies, few redirects, every connection address checked.</summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = Timeout,
        ConnectCallback = ConnectPubliclyAsync,
    };

    internal static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal);
        }

        var b = address.GetAddressBytes();
        return b[0] switch
        {
            0 or 10 or 127 => false,
            100 when b[1] is >= 64 and <= 127 => false,   // carrier-grade NAT
            169 when b[1] == 254 => false,                 // link-local, incl. the cloud metadata endpoint
            172 when b[1] is >= 16 and <= 31 => false,
            192 when b[1] == 168 => false,
            192 when b[1] == 0 && b[2] == 0 => false,
            198 when b[1] is 18 or 19 => false,
            >= 224 => false,                               // multicast and reserved
            _ => true,
        };
    }

    private static async ValueTask<Stream> ConnectPubliclyAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var target = addresses.FirstOrDefault(IsPublic);
        if (target is null || addresses.Any(a => !IsPublic(a)))
        {
            throw new BlockedAddressException();
        }

        var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(target, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxBodyBytes];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static string StripTags(string html) => TagPattern().Replace(html, " ");

    /// <summary>Lower case, entities for spaces removed, runs of whitespace collapsed.</summary>
    private static string Normalize(string text) =>
        WhitespacePattern().Replace(WebUtility.HtmlDecode(text).ToLowerInvariant(), " ").Trim();

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>|<[^>]+>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TagPattern();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [LoggerMessage(Level = LogLevel.Information, Message = "Credential link check failed {Reason}")]
    private static partial void LogFailed(ILogger logger, string reason);

    private sealed class BlockedAddressException() : HttpRequestException("The link points to a private network address.");
}
