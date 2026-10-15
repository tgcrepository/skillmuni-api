using Gamification.Application;
using Gamification.Infrastructure.Persistence;
using SkillMuni.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniServiceDefaults("gamification");

var services = builder.Services;
services.AddOptions<GamificationOptions>().BindConfiguration(GamificationOptions.Section);
services.AddGamificationApplication();
services.AddGamificationInfrastructure(builder.Configuration);
services.AddHealthChecks().AddDbContextCheck<GamificationDbContext>("mysql", tags: [WebDefaults.ReadyTag]);

var app = builder.Build();
app.UseSkillMuniServiceDefaults();

// Local runs and tests only; production applies migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateGamificationDatabaseAsync();
}

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
