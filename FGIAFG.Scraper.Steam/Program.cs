using FGIAFG.Scraper.Steam.Database;
using FGIAFG.Scraper.Steam.Jobs;
using FGIAFG.Scraper.Steam.Scraping;
using FGIAFG.Scraper.Steam.SteamApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using Serilog;
using SteamKit2;
using DbContext = FGIAFG.Scraper.Steam.Database.DbContext;

namespace FGIAFG.Scraper.Steam;

internal class Program
{
    private const string JOB_KEY = "SteamScraperJob";
    private const string TRIGGER_IDENTITY = JOB_KEY + "-trigger";

    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration
                .WriteTo.Console()
                .MinimumLevel.Debug();
        });

        builder.Services.AddDbContext<DbContext>(options =>
        {
            string path = Path.GetDirectoryName(builder.Configuration["DataSource"])!;
            if (!string.IsNullOrEmpty(path) && !Path.Exists(path))
                Directory.CreateDirectory(path);

            options.UseSqlite("Data Source=" + builder.Configuration["DataSource"]);
        });
        builder.Services.AddTransient<SteamScraper>();
        builder.Services.AddHttpClient();

        builder.Services.AddQuartz(q =>
        {
            JobKey key = new JobKey("ScrapeAndStoreJob");
            q.AddJob<ScrapeAndStoreJob>(o => o.WithIdentity(key));
            q.AddTrigger(o =>
                o.ForJob(key).WithIdentity("ScrapeAndStoreTrigger")
                    .WithCronSchedule(builder.Configuration["Schedule"] ?? "0 0/15 * ? * * *"));
            q.AddTrigger(o => o.ForJob(key).StartAt(DateTimeOffset.UtcNow.AddSeconds(15)));
            q.UseMicrosoftDependencyInjectionJobFactory();

            // JobKey jobKey = new JobKey(JOB_KEY);

            q.UseMicrosoftDependencyInjectionJobFactory();
            // q.AddJob<SteamScraperJob>(opts => opts
            //     .WithIdentity(jobKey)
            //     .DisallowConcurrentExecution());
            // q.AddTrigger(opts =>
            // {
            //     opts.ForJob(jobKey)
            //         .WithIdentity(TRIGGER_IDENTITY)
            //         .WithCronSchedule("0 * * ? * * *",
            //             builder => builder.WithMisfireHandlingInstructionDoNothing());
            // });
        });

        builder.Services.AddQuartzHostedService();

        builder.Services.AddSingleton<SteamClient>(CreateSteamClient);
        builder.Services.AddSingleton<CallbackManager>(CreateCallbackManager);
        builder.Services.AddSingleton<SteamConnector>();

        builder.Services.AddHostedService<SteamConnectionHandler>();
        builder.Services.AddHostedService<SteamCallbackWorker>();

        builder.Services.Configure<SteamOptions>(builder.Configuration.GetSection("Steam"));

        WebApplication app = builder.Build();

        using (IServiceScope scope = app.Services.CreateScope())
        {
            DbContext dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
            ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
#if DEBUG

            logger.LogInformation("Deleting database...");
            await dbContext.Database.EnsureDeletedAsync();
#endif
            logger.LogInformation("Creating database...");
            await dbContext.Database.EnsureCreatedAsync();
        }

        app.MapGet("/", GetGames);

        await app.RunAsync();
    }

    private static Task<IResult> GetGames(DbContext dbContext, HttpContext context)
    {
        IQueryable<GameModel> gameModels =
            dbContext.Games.Where(x => x.StartDate <= DateTime.Now && x.EndDate >= DateTime.Now);

        return Task.FromResult<IResult>(TypedResults.Ok(gameModels));
    }

    private static CallbackManager CreateCallbackManager(IServiceProvider provider)
    {
        return new CallbackManager(provider.GetRequiredService<SteamClient>());
    }

    private static SteamClient CreateSteamClient(IServiceProvider provider)
    {
        IOptions<SteamOptions> options = provider.GetRequiredService<IOptions<SteamOptions>>();

        return new SteamClient(SteamConfiguration.Create(builder =>
            builder.WithWebAPIKey(options.Value.ApiKey)));
    }
}