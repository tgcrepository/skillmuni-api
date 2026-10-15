using Assessment.Application;
using Assessment.Infrastructure.Persistence;
using SkillMuni.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniServiceDefaults("assessment");

var services = builder.Services;
services.AddOptions<AssessmentOptions>().BindConfiguration(AssessmentOptions.Section);
services.AddAssessmentApplication();
services.AddAssessmentInfrastructure(builder.Configuration);
services.AddHealthChecks().AddDbContextCheck<AssessmentDbContext>("mysql", tags: [WebDefaults.ReadyTag]);

var app = builder.Build();
app.UseSkillMuniServiceDefaults();

// Local runs and tests only; production applies migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateAssessmentDatabaseAsync();
}

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
