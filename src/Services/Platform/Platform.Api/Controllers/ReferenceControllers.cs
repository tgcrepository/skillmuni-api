using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Platform.Application;
using Platform.Application.Services;
using SkillMuni.Web.Errors;

namespace Platform.Api.Controllers;

/// <summary>API-032/033: address lookup for Q2 Hit Me Up. Public reference data, output-cached.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/geo")]
[Produces("application/json")]
[AllowAnonymous]
[OutputCache(PolicyName = OutputCachePolicies.PublicReference)]
public sealed class GeoController(IGeoService geo) : ControllerBase
{
    /// <summary><c>market</c>: IN · ZA. 404 <c>postal-not-found</c> → the app lets the learner pick the state.</summary>
    [HttpGet("{market}/postal/{code}")]
    public async Task<IActionResult> Postal(string market, string code, CancellationToken cancellationToken) =>
        (await geo.LookupAsync(market, code, cancellationToken)).ToActionResult(this);

    /// <summary>States / UTs (IN) or provinces (ZA); an unknown market gets <c>[]</c>.</summary>
    [HttpGet("{market}/regions")]
    public async Task<ActionResult<IReadOnlyList<string>>> Regions(string market, CancellationToken cancellationToken) =>
        Ok(await geo.RegionsAsync(market, cancellationToken));
}

/// <summary>MISS-12: the tenant (from the gateway's <c>X-Tenant-Id</c>) and its market, served before sign-in.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/tenant")]
[Produces("application/json")]
[AllowAnonymous]
[OutputCache(PolicyName = OutputCachePolicies.PublicReference)]
public sealed class TenantController(ITenantConfigService config) : ControllerBase
{
    /// <summary>The app's <c>TenantTheme</c>: <c>{ id, name, brand?, accent?, logoUrl? }</c>.</summary>
    [HttpGet("theme")]
    public async Task<ActionResult<TenantThemeDto>> Theme(CancellationToken cancellationToken) =>
        await config.ThemeAsync(cancellationToken);

    /// <summary>The app's <c>MarketConfig</c>, patterns as regex sources. <c>?code=ZA</c> reads another market.</summary>
    [HttpGet("market")]
    public IActionResult Market([FromQuery] string? code) => config.Market(code).ToActionResult(this);
}
