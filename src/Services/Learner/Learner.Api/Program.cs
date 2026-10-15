using Learner.Application;
using Learner.Infrastructure.Persistence;
using SkillMuni.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniServiceDefaults("learner");

var services = builder.Services;
services.AddOptions<LearnerServiceOptions>().BindConfiguration(LearnerServiceOptions.Section);
services.AddLearnerApplication();
services.AddLearnerInfrastructure(builder.Configuration, builder.Environment);
services.AddHealthChecks().AddDbContextCheck<LearnerDbContext>("mysql", tags: [WebDefaults.ReadyTag]);

var app = builder.Build();
app.UseSkillMuniServiceDefaults();

// Local runs and tests only; production applies migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateLearnerDatabaseAsync();
}

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
