using SteamKit2;

namespace FGIAFG.Scraper.Steam.SteamApi;

public class SteamConnector
{
    private readonly SteamClient steamClient;
    private readonly CallbackManager callbackManager;
    private readonly SteamUser steamUser;

    public bool IsConnected { get; private set; }
    public bool IsDisconnected { get; private set; }
    public bool IsLoggedOn { get; private set; }
    public bool IsLoggedOff { get; private set; }

    public SteamConnector(SteamClient steamClient, CallbackManager callbackManager)
    {
        this.steamClient = steamClient;
        this.callbackManager = callbackManager;

        steamUser = this.steamClient.GetHandler<SteamUser>()!;

        callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        callbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback obj)
    {
        IsLoggedOff = true;
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback obj)
    {
        IsLoggedOn = true;
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback obj)
    {
        IsDisconnected = true;
    }

    private void OnConnected(SteamClient.ConnectedCallback obj)
    {
        IsConnected = true;
    }

    public async Task Connect()
    {
        if (IsConnected)
            return;

        IsConnected = false;
        IsDisconnected = false;
        IsLoggedOff = false;
        IsLoggedOn = false;

        steamClient.Connect();

        while (!IsConnected)
        {
            await Task.Yield();
        }
    }

    public async Task Disconnect()
    {
        if (!IsConnected || IsDisconnected)
            return;

        steamClient.Disconnect();

        while (!IsDisconnected)
        {
            await Task.Yield();
        }
    }

    public async Task LogOn()
    {
        if (IsLoggedOn)
            return;

        steamUser.LogOnAnonymous();

        while (!IsLoggedOn)
        {
            await Task.Yield();
        }
    }

    public async Task LogOff()
    {
        if (!IsLoggedOn || IsLoggedOff)
            return;
        
        steamUser.LogOff();

        while (!IsLoggedOff)
        {
            await Task.Yield();
        }
    }
}
