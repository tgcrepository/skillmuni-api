using Identity.Api.Http;
using Identity.Application;
using Identity.Application.Abstractions;
using Identity.Infrastructure.Persistence;
using SkillMuni.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniServiceDefaults("identity");

var services = builder.Services;
services.AddOptions<IdentityServiceOptions>().BindConfiguration(IdentityServiceOptions.Section);
services.AddIdentityApplication();
services.AddIdentityInfrastructure(builder.Environment);
services.AddScoped<ICurrentUser, HttpCurrentUser>();
services.AddScoped<ITenantSettings, HttpTenantSettings>();
services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("mysql", tags: [WebDefaults.ReadyTag]);

var app = builder.Build();
app.UseSkillMuniServiceDefaults();

// Local runs and tests only; production applies migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateIdentityDatabaseAsync();
}

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
