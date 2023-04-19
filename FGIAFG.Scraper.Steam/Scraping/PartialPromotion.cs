namespace FGIAFG.Scraper.Steam.Scraping;

internal struct PartialPromotion
{
    public uint AppId;
    public DateTimeOffset StartTime;
    public DateTimeOffset ExpiryTime;

    public PartialPromotion(string appId, string startTime, string expiryTime)
    {
        AppId = uint.Parse(appId);
        StartTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(startTime));
        ExpiryTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiryTime));
    }
}
