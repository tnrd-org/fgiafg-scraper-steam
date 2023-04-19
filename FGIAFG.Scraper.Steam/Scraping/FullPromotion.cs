namespace FGIAFG.Scraper.Steam.Scraping;

internal struct FullPromotion
{
    public uint AppId;
    public DateTimeOffset StartTime;
    public DateTimeOffset ExpiryTime;
    public string Name;
    public string Image;

    public FullPromotion(PartialPromotion partialPromotion, string name, string image)
    {
        AppId = partialPromotion.AppId;
        StartTime = partialPromotion.StartTime;
        ExpiryTime = partialPromotion.ExpiryTime;
        Name = name;
        Image = image;
    }
}
