using MediaManagement.Implementation.Services;
using MediaManagement.Models;
using Microsoft.Extensions.Options;

namespace MediaManagement.BackgroundWorkers;

public sealed class UploadCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<UploadOptions> options,
    ILogger<UploadCleanupWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.CleanupEnabled)
        {
            return;
        }

        using PeriodicTimer timer = new(TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes));
        do
        {
            try
            {
                int count;
                do
                {
                    await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                    count = await scope
                        .ServiceProvider.GetRequiredService<UploadCleanupService>()
                        .RunBatchAsync(stoppingToken);
                } while (
                    count == options.Value.CleanupBatchSize
                    && !stoppingToken.IsCancellationRequested
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Upload cleanup pass failed; will retry");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
