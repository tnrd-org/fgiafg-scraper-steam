namespace FGIAFG.Scraper.Steam.SteamApi;

public class SteamConnectionHandler : BackgroundService
{
    private readonly ILogger<SteamConnectionHandler> logger;
    private readonly SteamConnector steamConnector;

    public SteamConnectionHandler(ILogger<SteamConnectionHandler> logger, SteamConnector steamConnector)
    {
        this.logger = logger;
        this.steamConnector = steamConnector;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Connecting");
        await steamConnector.Connect();
        logger.LogInformation("Logging on");
        await steamConnector.LogOn();
        logger.LogInformation("Logged on");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Yield();
        }

        logger.LogInformation("Disconnecting");
        await steamConnector.Disconnect();
    }
}
