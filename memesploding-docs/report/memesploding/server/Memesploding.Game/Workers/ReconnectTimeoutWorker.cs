using Memesploding.Game.Infrastructure.Runtime;

namespace Memesploding.Game.Workers;

public class ReconnectTimeoutWorker(
    IMatchRuntimeManager runtimeManager,
    ILogger<ReconnectTimeoutWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ReconnectTimeoutWorker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await runtimeManager.ProcessReconnectTimeoutsAsync(stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
