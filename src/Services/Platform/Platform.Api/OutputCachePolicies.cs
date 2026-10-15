namespace Platform.Api;

public static class OutputCachePolicies
{
    /// <summary>Anonymous reference GETs: 10 minutes, varied by tenant header and <c>?code</c>.</summary>
    public const string PublicReference = "public-reference";
}
