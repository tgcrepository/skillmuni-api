using Cv.Application;
using Cv.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Tenancy;

namespace Cv.Infrastructure.Worker;

/// <summary>Wakes this replica's worker at once after an upload; other replicas find the job on their next poll.</summary>
internal sealed class CvParseSignal : ICvParseSignal, IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        if (_signal.CurrentCount == 0)
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already signalled.
            }
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _signal.WaitAsync(timeout, cancellationToken);

    public void Dispose() => _signal.Dispose();
}

/// <summary>
/// Parses queued CVs one at a time per replica (scale out by replicas), requeues jobs a crashed worker left behind,
/// and deletes original files once they are past <see cref="CvServiceOptions.KeepOriginalFor"/>.
/// </summary>
internal sealed partial class CvParseWorker(
    IServiceScopeFactory scopes,
    CvParseSignal signal,
    TimeProvider clock,
    IOptions<CvServiceOptions> options,
    ILogger<CvParseWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SweepEvery = TimeSpan.FromHours(1);
    private const int SweepBatch = 100;

    private DateTime _nextSweep = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await RunNextAsync(stoppingToken))
                {
                    continue;
                }

                await SweepIfDueAsync(stoppingToken);
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                LogLoopFailed(logger, e);
            }

            try
            {
                await signal.WaitAsync(options.Value.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<bool> RunNextAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        ClaimedJob? job;
        await using (var scope = scopes.CreateAsyncScope())
        {
            job = await scope.ServiceProvider.GetRequiredService<ICvJobQueue>()
                .ClaimNextAsync(now, now - settings.StaleAfter, settings.MaxAttempts, cancellationToken);
        }

        if (job is null)
        {
            return false;
        }

        await using (var scope = scopes.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(job.TenantId);
            await scope.ServiceProvider.GetRequiredService<ICvParseJob>().RunAsync(job.CvId, cancellationToken);
        }

        return true;
    }

    private async Task SweepIfDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (now < _nextSweep)
        {
            return;
        }

        _nextSweep = now + SweepEvery;
        await using var scope = scopes.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<ICvJobQueue>();
        var blobs = scope.ServiceProvider.GetRequiredService<IBlobStore>();
        var expired = await queue.ListExpiredFilesAsync(now - options.Value.KeepOriginalFor, SweepBatch, cancellationToken);
        foreach (var files in expired)
        {
            foreach (var name in files.BlobNames)
            {
                await blobs.DeleteAsync(name, cancellationToken);
            }

            await queue.ForgetFilesAsync(files.CvId, cancellationToken);
        }

        if (expired.Count > 0)
        {
            LogSwept(logger, expired.Count);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "CV worker loop failed")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted original CV files {Count}")]
    private static partial void LogSwept(ILogger logger, int count);
}
