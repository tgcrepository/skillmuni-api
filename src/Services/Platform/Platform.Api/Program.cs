using Platform.Api;
using Platform.Application;
using Platform.Infrastructure.Persistence;
using SkillMuni.Web.Hosting;
using SkillMuni.Web.Tenancy;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniServiceDefaults("platform");

var services = builder.Services;
services.AddOptions<PlatformOptions>().BindConfiguration(PlatformOptions.Section);
services.AddPlatformApplication();
services.AddPlatformInfrastructure(builder.Configuration);
services.AddHealthChecks().AddDbContextCheck<PlatformDbContext>("mysql", tags: [WebDefaults.ReadyTag]);

// Public reference GETs (theme, market, geo) are output-cached per tenant (CLAUDE.md §8). Signed-in requests skip it.
services.AddOutputCache(o => o.AddPolicy(OutputCachePolicies.PublicReference, b => b
    .Expire(TimeSpan.FromMinutes(10))
    .SetVaryByHeader(TenancyOptions.TenantHeader)
    .SetVaryByQuery("code")));

var app = builder.Build();
app.UseSkillMuniServiceDefaults();
app.UseOutputCache();

// Local runs and tests only; production applies migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigratePlatformDatabaseAsync();
}

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
