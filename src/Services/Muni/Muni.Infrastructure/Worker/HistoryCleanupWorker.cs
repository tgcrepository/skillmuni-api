using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muni.Application;

namespace Muni.Infrastructure.Worker;

/// <summary>
/// Retention: every <c>Muni:Cleanup:Every</c>, deletes chat lines and ratings older than <c>Muni:HistoryDays</c>
/// (default 90; BRD §9 leaves the period open). Deletes are idempotent, so replicas running it together is harmless.
/// </summary>
internal sealed partial class HistoryCleanupWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<MuniOptions> options,
    ILogger<HistoryCleanupWorker> logger) : BackgroundService
{
    private const int MaxBatchesPerRun = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailed(logger, e);
            }

            try
            {
                await Task.Delay(options.Value.Cleanup.Every, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    internal async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-settings.HistoryDays);
        var total = 0;
        for (var i = 0; i < MaxBatchesPerRun; i++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var deleted = await scope.ServiceProvider.GetRequiredService<IChatRetention>().DeleteOlderThanAsync(cutoff, settings.Cleanup.Batch, cancellationToken);
            total += deleted;
            if (deleted == 0)
            {
                break;
            }
        }

        if (total > 0)
        {
            LogDeleted(logger, total, settings.HistoryDays);
        }

        return total;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Muni history cleanup failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Muni history cleanup deleted {Rows} rows older than {Days} days")]
    private static partial void LogDeleted(ILogger logger, int rows, int days);
}
