using Learning.Application;
using Learning.Infrastructure.Persistence;
using SkillMuni.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniServiceDefaults("learning");

var services = builder.Services;
services.AddOptions<LearningOptions>().BindConfiguration(LearningOptions.Section);
services.AddLearningApplication();
services.AddLearningInfrastructure(builder.Configuration);
services.AddHealthChecks().AddDbContextCheck<LearningDbContext>("mysql", tags: [WebDefaults.ReadyTag]);

var app = builder.Build();
app.UseSkillMuniServiceDefaults();

// Local runs and tests only; production applies migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateLearningDatabaseAsync();
}

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
