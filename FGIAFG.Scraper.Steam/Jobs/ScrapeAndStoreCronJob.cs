using FGIAFG.Scraper.Steam.Database;
using FGIAFG.Scraper.Steam.Scraping;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Quartz;
using DbContext = FGIAFG.Scraper.Steam.Database.DbContext;

namespace FGIAFG.Scraper.Steam.Jobs;

internal class ScrapeAndStoreCronJob
{
    private readonly SteamScraper scraper;
    private readonly DbContext dbContext;
    private readonly ILogger<ScrapeAndStoreCronJob> logger;

    public ScrapeAndStoreCronJob(SteamScraper scraper, DbContext dbContext, ILogger<ScrapeAndStoreCronJob> logger)
    {
        this.scraper = scraper;
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task Execute(CancellationToken ct)
    {
        logger.LogInformation("Starting job");

        Result<IEnumerable<FreeGame>> result = await scraper.Scrape(ct);
        if (result.IsFailed)
        {
            logger.LogError("Scrape failed. Result: {Result}", result.ToString());
            return;
        }
        
        logger.LogInformation("Got {Count} games", result.Value.Count());

        if (ct.IsCancellationRequested)
        {
            logger.LogInformation("Cancellation requested");
            return;
        }

        foreach (FreeGame freeGame in result.Value)
        {
            if (ct.IsCancellationRequested)
                return;

            string hash = freeGame.CalculatePersistentHash();

            if (await dbContext.Games.AnyAsync(x => x.Hash == hash, ct))
                continue;

            EntityEntry<GameModel> entry = dbContext.Games.Add(new GameModel()
            {
                Title = freeGame.Title,
                EndDate = freeGame.EndDate,
                Url = freeGame.Url,
                ImageUrl = freeGame.ImageUrl,
                StartDate = freeGame.StartDate,
                Hash = hash
            });
        }

        logger.LogInformation("Saving changes");
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Done saving changes");
    }
}
