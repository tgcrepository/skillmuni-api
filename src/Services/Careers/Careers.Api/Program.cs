using Careers.Application;
using Careers.Infrastructure.Persistence;
using SkillMuni.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniServiceDefaults("careers");

var services = builder.Services;
services.AddOptions<CareersOptions>().BindConfiguration(CareersOptions.Section);
services.AddCareersApplication();
services.AddCareersInfrastructure(builder.Configuration, builder.Environment);
services.AddHealthChecks().AddDbContextCheck<CareersDbContext>("mysql", tags: [WebDefaults.ReadyTag]);

var app = builder.Build();
app.UseSkillMuniServiceDefaults();

// Local runs and tests only; production applies migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateCareersDatabaseAsync();
}

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
