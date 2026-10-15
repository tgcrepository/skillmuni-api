using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learner.Application.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Learner.Infrastructure.External;

/// <summary>
/// India IFSC lookup through Razorpay's free public API (<c>https://ifsc.razorpay.com/{IFSC}</c>, no key).
/// Branches change rarely, so answers (including "not found") are cached for a week.
/// </summary>
internal sealed partial class RazorpayBankDirectory(HttpClient http, IDistributedCache cache, ILogger<RazorpayBankDirectory> logger) : IBankDirectory
{
    public const string BaseAddress = "https://ifsc.razorpay.com/";

    private const string Missing = "-";
    private static readonly DistributedCacheEntryOptions CacheFor = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) };

    public async Task<Lookup<BankBranchInfo>> FindIfscAsync(string ifsc, CancellationToken cancellationToken)
    {
        var key = $"learner:ifsc:{ifsc}";
        if (await cache.GetStringAsync(key, cancellationToken) is { } cached)
        {
            return cached == Missing
                ? Lookup<BankBranchInfo>.NotFound()
                : Lookup<BankBranchInfo>.Found(JsonSerializer.Deserialize<BankBranchInfo>(cached)!);
        }

        try
        {
            using var response = await http.GetAsync(Uri.EscapeDataString(ifsc), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await cache.SetStringAsync(key, Missing, CacheFor, cancellationToken);
                return Lookup<BankBranchInfo>.NotFound();
            }

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<IfscResponse>(cancellationToken);
            if (body?.Bank is not { Length: > 0 } bank)
            {
                return Lookup<BankBranchInfo>.Unavailable();
            }

            var branch = new BankBranchInfo(bank, string.Join(", ", new[] { body.Branch, body.City }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct()));
            await cache.SetStringAsync(key, JsonSerializer.Serialize(branch), CacheFor, cancellationToken);
            return Lookup<BankBranchInfo>.Found(branch);
        }
        catch (Exception e) when (ExternalCall.Failed(e, cancellationToken))
        {
            LogUnavailable(logger, e);
            return Lookup<BankBranchInfo>.Unavailable();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "IFSC lookup unavailable")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);

    private sealed record IfscResponse(
        [property: JsonPropertyName("BANK")] string? Bank,
        [property: JsonPropertyName("BRANCH")] string? Branch,
        [property: JsonPropertyName("CITY")] string? City);
}
