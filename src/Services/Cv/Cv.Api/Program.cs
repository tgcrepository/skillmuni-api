using Cv.Application;
using Cv.Infrastructure.Persistence;
using SkillMuni.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniServiceDefaults("cv");

var services = builder.Services;
services.AddOptions<CvServiceOptions>().BindConfiguration(CvServiceOptions.Section);
services.AddCvApplication();
services.AddCvInfrastructure(builder.Configuration, builder.Environment);
services.AddHealthChecks().AddDbContextCheck<CvDbContext>("mysql", tags: [WebDefaults.ReadyTag]);

var app = builder.Build();
app.UseSkillMuniServiceDefaults();

// Local runs and tests only; production applies migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateCvDatabaseAsync();
}

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
