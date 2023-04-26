using FGIAFG.Scraper.Steam.SteamApi;
using FluentResults;
using SteamKit2;

namespace FGIAFG.Scraper.Steam.Scraping;

internal class SteamScraper
{
    private readonly ILogger<SteamScraper> logger;

    private readonly SteamClient client;
    private readonly SteamConnector steamConnector;
    private readonly Steamoptions steamOptions;

    private static uint? LastChangeNumber
    {
        get
        {
            if (string.IsNullOrEmpty(steamOptions.Changelist))
                return null;

            if (uint.Tryparse(steamOptions.Changelist, out uint parsed))
                return parsed;

            return null;
        }
        set
        {
            steamOptions.Changelist = value.ToString();
        }
    }

    public SteamScraper(ILogger<SteamScraper> logger, SteamClient client, SteamConnector steamConnector, SteamOptions steamOptions)
    {
        this.logger = logger;
        this.client = client;
        this.steamConnector = steamConnector;
        this.steamOptions = steamOptions;
    }

    public async Task<Result<IEnumerable<FreeGame>>> Scrape(CancellationToken cancellationToken)
    {
        if (!steamConnector.IsLoggedOn || !steamConnector.IsConnected)
        {
            logger.LogWarning("Skipping job because not connected or logged on");
            return new List<FreeGame>();
        }

        logger.LogInformation("Getting changes");
        SteamApps.PICSChangesCallback changes = await GetChanges();
        LastChangeNumber = changes.CurrentChangeNumber;

        logger.LogInformation("Getting product info");
        SteamApps steamApps = client.GetHandler<SteamApps>()!;
        AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet productInfos = await steamApps.PICSGetProductInfo(
            changes.AppChanges.Select(x => new SteamApps.PICSRequest(x.Key)),
            changes.PackageChanges.Select(x => new SteamApps.PICSRequest(x.Key)));

        if (productInfos.Failed)
        {
            logger.LogError("Failed to get product info, disconnecting");
            await steamConnector.Disconnect();
            return new List<FreeGame>();
        }

        logger.LogInformation("Parsing product info");
        List<PartialPromotion> promotions = ParseProductInfos(productInfos);

        logger.LogInformation("Parsing partial promotions");
        List<FullPromotion> fullPromotions = await ParsePartialPromotions(promotions);

        logger.LogInformation("Found {Amount} promotions", fullPromotions.Count);

        List<FreeGame> freeGames = new List<FreeGame>();

        foreach (FullPromotion fullPromotion in fullPromotions)
        {
            freeGames.Add(new FreeGame(fullPromotion.Name,
                fullPromotion.Image,
                "https://store.steampowered.com/app/" + fullPromotion.AppId,
                fullPromotion.StartTime.DateTime,
                fullPromotion.ExpiryTime.DateTime));
        }

        return Result.Ok(freeGames.AsEnumerable());
    }

    private async Task<SteamApps.PICSChangesCallback> GetChanges()
    {
        SteamApps steamApps = client.GetHandler<SteamApps>()!;
        SteamApps.PICSChangesCallback? changes = default;

        if (LastChangeNumber == null)
        {
            changes = await steamApps.PICSGetChangesSince(0, true, true);
        }
        else
        {
            changes = await steamApps.PICSGetChangesSince(LastChangeNumber.Value, true, true);
        }

        return changes;
    }

    private List<PartialPromotion> ParseProductInfos(
        AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet productInfos
    )
    {
        List<PartialPromotion> promotions = new List<PartialPromotion>();

        foreach (SteamApps.PICSProductInfoCallback result in productInfos.Results)
        {
            foreach ((uint key, SteamApps.PICSProductInfoCallback.PICSProductInfo? value) in result.Apps)
            {
                // Not sure if this one works though...
                if (TryGetPartialPromotion(value, out PartialPromotion partialPromotion))
                {
                    promotions.Add(partialPromotion);
                }
            }

            foreach ((uint key, SteamApps.PICSProductInfoCallback.PICSProductInfo? value) in result.Packages)
            {
                if (TryGetPartialPromotion(value, out PartialPromotion partialPromotion))
                {
                    promotions.Add(partialPromotion);
                }
            }
        }

        return promotions;
    }

    private static bool TryGetPartialPromotion(
        SteamApps.PICSProductInfoCallback.PICSProductInfo productInfo,
        out PartialPromotion partialPromotion
    )
    {
        bool hasFreePromotion = productInfo.KeyValues.Children.FirstOrDefault(x => x.Name == "extended")?.Children
            .FirstOrDefault(y => y.Name == "freepromotion") != null;

        if (!hasFreePromotion)
        {
            partialPromotion = default;
            return false;
        }

        KeyValueDict keyValueDict = new KeyValueDict(productInfo.KeyValues);

        string appId = keyValueDict["appids"]["0"].Value;
        string startTime = keyValueDict["extended"]["starttime"].Value;
        string expiryTime = keyValueDict["extended"]["expirytime"].Value;

        partialPromotion = new PartialPromotion(appId, startTime, expiryTime);
        return true;
    }

    private async Task<List<FullPromotion>> ParsePartialPromotions(List<PartialPromotion> promotions)
    {
        SteamApps steamApps = client.GetHandler<SteamApps>()!;
        List<FullPromotion> fullPromotionInfos = new List<FullPromotion>();

        foreach (PartialPromotion promotionInfo in promotions)
        {
            AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet result =
                await steamApps.PICSGetProductInfo(new SteamApps.PICSRequest(promotionInfo.AppId), null, false);

            if (result.Failed)
                continue;

            KeyValueDict kv = new KeyValueDict(result.Results.First().Apps[promotionInfo.AppId].KeyValues);

            if (!kv.Contains("common"))
                continue;

            string name = kv["common"]["name"].Value;
            string? headerImage = GetHeaderImage(kv["common"]["header_image"], promotionInfo);

            FullPromotion fullPromotion = new FullPromotion(promotionInfo, name, headerImage);
            fullPromotionInfos.Add(fullPromotion);
        }

        return fullPromotionInfos;
    }

    private static string? GetHeaderImage(KeyValueDict headerImg, PartialPromotion promotionInfo)
    {
        string? headerImage = null;

        if (headerImg.Children.TryGetValue("english", out KeyValueDict? e))
        {
            headerImage = "https://cdn.cloudflare.steamstatic.com/steam/apps/" +
                          promotionInfo.AppId +
                          "/" +
                          e.Value;
        }
        else
        {
            KeyValuePair<string, KeyValueDict> kvp = headerImg.Children.FirstOrDefault();
            if (!string.IsNullOrEmpty(kvp.Key))
            {
                headerImage = "https://cdn.cloudflare.steamstatic.com/steam/apps/" +
                              promotionInfo.AppId +
                              "/" +
                              kvp.Value;
            }
        }

        return headerImage;
    }
}
