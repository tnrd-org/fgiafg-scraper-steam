using NCrontab;

namespace FGIAFG.Scraper.Steam.Jobs;

internal class Scheduler : BackgroundService
{
    private readonly IServiceProvider provider;
    private readonly CrontabSchedule schedule;
    private readonly ILogger<Scheduler> logger;

    public Scheduler(IServiceProvider provider, IConfiguration configuration, ILogger<Scheduler> logger)
    {
        this.provider = provider;
        this.logger = logger;

        string cron = configuration["Schedule"] ?? "0 0/15 * * *";
        schedule = CrontabSchedule.TryParse(cron,
            new CrontabSchedule.ParseOptions()
            {
                IncludingSeconds = true
            });
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan timeToWait = schedule.GetNextOccurrence(DateTime.Now) - DateTime.Now;

            try
            {
                logger.LogInformation("Waiting for {Delay}", timeToWait);
                await Task.Delay(timeToWait, stoppingToken);
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                using (IServiceScope scope = provider.CreateScope())
                {
                    ScrapeAndStoreCronJob job = scope.ServiceProvider.GetRequiredService<ScrapeAndStoreCronJob>();
                    await job.Execute(stoppingToken);
                }
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "Error executing job");
            }
        }
    }
}
