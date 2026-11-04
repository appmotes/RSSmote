using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using RSSmote.Core.Api;
using RSSmote.Core.Discovery;
using RSSmote.Data;
using RSSmote.Server;

var builder = WebApplication.CreateBuilder(args);

// RSSMOTE_DATA_DIR from the environment or the command line; the per-OS folder otherwise (A4).
var data = builder.Configuration["RSSMOTE_DATA_DIR"] is { Length: > 0 } configured ? configured : AppFolders.Data();
Directory.CreateDirectory(data);

builder.Services.AddAccess(AccessKey.From(builder.Configuration));
builder.Services.AddDataProtection()
    .SetApplicationName("RSSmote")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(data, "keys")));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(Access.LoginPolicy, http => RateLimitPartition.GetFixedWindowLimiter(ClientAddress(http),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
        }));
    // Every API call, per address: bursts of favicons pass, a flood does not.
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        http.Request.Path.StartsWithSegments("/api")
            ? RateLimitPartition.GetTokenBucketLimiter(ClientAddress(http), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 600,
                TokensPerPeriod = 60,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            })
            : RateLimitPartition.GetNoLimiter(""));
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(_ => FeedFinder.CreateHttpClient());
builder.Services.AddSingleton(sp => new FeedFinder(sp.GetRequiredService<HttpClient>()));
builder.Services.AddSingleton(sp => new ImageProxy(sp.GetRequiredService<HttpClient>()));
builder.Services.AddSingleton(sp => new Store(Path.Combine(data, AppFolders.DatabaseFile), sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(_ => new SettingsStore(Path.Combine(data, AppFolders.SettingsFile)));
builder.Services.AddSingleton(sp => new Refresher(sp.GetRequiredService<Store>(), sp.GetRequiredService<FeedFinder>(),
    sp.GetRequiredService<SettingsStore>(), sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<LibraryEvents>();
builder.Services.AddHostedService<RefreshService>();

var app = builder.Build();

// The published browser app: RSSMOTE_WEB_DIR, or wwwroot beside the server (A24). Without it the server is the API alone.
var web = builder.Configuration["RSSMOTE_WEB_DIR"] is { Length: > 0 } folder ? folder : Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(web))
{
    app.UseWebApp(web);
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");
api.AddEndpointFilter(async (context, next) =>
{
    // A write must name its client: a cross-site form cannot set a header, so this also stops forged writes.
    var request = context.HttpContext.Request;
    var client = request.Headers[ApiNames.ClientHeader].ToString();
    if (client.Length == 0 && !HttpMethods.IsGet(request.Method))
    {
        return Results.BadRequest(new ErrorResponse($"{ApiNames.ClientHeader} is required."));
    }

    RequestOrigin.Current = client.Length == 0 ? null : client;
    return await next(context);
});
api.MapAccess();
api.MapLibrary();

app.Run();

static string ClientAddress(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "";

namespace RSSmote.Server
{
    /// <summary>Refreshes once at start and then on the interval from settings, until the server stops.</summary>
    internal sealed class RefreshService(Refresher refresher, LibraryEvents events) : IHostedService
    {
        // LibraryEvents is taken so it follows the store from the first change.
        private readonly LibraryEvents _events = events;

        public Task StartAsync(CancellationToken ct)
        {
            refresher.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            refresher.Stop();
            return Task.CompletedTask;
        }
    }
}
