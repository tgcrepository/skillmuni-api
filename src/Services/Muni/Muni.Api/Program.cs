using Muni.Application;
using Muni.Infrastructure.Persistence;
using SkillMuni.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniServiceDefaults("muni");

var services = builder.Services;
services.AddOptions<MuniOptions>().BindConfiguration(MuniOptions.Section);
services.AddMuniApplication();
services.AddMuniInfrastructure(builder.Configuration, builder.Environment);
services.AddHealthChecks().AddDbContextCheck<MuniDbContext>("mysql", tags: [WebDefaults.ReadyTag]);

var app = builder.Build();
app.UseSkillMuniServiceDefaults();

// Local runs and tests only; production applies migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateMuniDatabaseAsync();
}

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
