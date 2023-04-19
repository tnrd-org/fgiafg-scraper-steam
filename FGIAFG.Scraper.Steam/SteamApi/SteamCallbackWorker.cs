using SteamKit2;

namespace FGIAFG.Scraper.Steam.SteamApi;

public class SteamCallbackWorker : BackgroundService
{
    private readonly CallbackManager callbackManager;

    /// <inheritdoc />
    public SteamCallbackWorker(CallbackManager callbackManager)
    {
        this.callbackManager = callbackManager;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            callbackManager.RunCallbacks();
            await Task.Yield();
        }
    }
}
