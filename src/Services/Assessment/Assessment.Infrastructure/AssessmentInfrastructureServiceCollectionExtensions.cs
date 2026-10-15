using Assessment.Application;
using Assessment.Application.Handlers;
using Assessment.Infrastructure.Content;
using Assessment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class AssessmentInfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Assessment";

    /// <summary>Every event type the "assessment" subscription receives (kept with the handler list).</summary>
    public static readonly IReadOnlyList<string> SubscribedEvents = [AccountRegistered.Name, AccountErased.Name];

    /// <summary>MySQL, repositories, the survey content, outbox and event handlers.</summary>
    public static IServiceCollection AddAssessmentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AssessmentDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
            options.UseMySQL(connectionString, mysql => mysql
                .MigrationsHistoryTable(AssessmentDatabase.HistoryTable)
                .EnableRetryOnFailure(3));
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AssessmentDbContext>());
        services.AddScoped<ICareerJourneyRepository, CareerJourneyRepository>();
        services.AddScoped<ICareerAttemptRepository, CareerAttemptRepository>();
        services.AddScoped<IAssessmentDataEraser, AssessmentDataEraser>();
        services.AddSingleton<ICareerDriversContentProvider, EmbeddedCareerDriversContent>();

        services.AddSkillMuniMessaging<AssessmentDbContext>();
        services.AddSkillMuniEventConsumer<AssessmentDbContext>();
        services.AddIntegrationEventHandler<AccountRegistered, AccountRegisteredHandler>(AccountRegistered.Name);
        services.AddIntegrationEventHandler<AccountErased, AccountErasedHandler>(AccountErased.Name);
        return services;
    }
}
