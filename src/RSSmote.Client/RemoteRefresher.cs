using RSSmote.Core.Api;
using RSSmote.Core.Discovery;
using RSSmote.Core.Library;

namespace RSSmote.Client;

/// <summary>The server's refresh: started here, its state reported back by the server.</summary>
public sealed class RemoteRefresher(ApiClient api) : IRefresher
{
    private volatile bool _refreshing;

    public event EventHandler? StateChanged;

    public bool IsRefreshing => _refreshing;

    /// <summary>Completes once the server has started the pass; its end arrives as a state change.</summary>
    public Task RefreshAllAsync() => api.SendAsync(HttpMethod.Post, "api/refresh");

    public void Apply(bool refreshing)
    {
        if (_refreshing == refreshing)
        {
            return;
        }

        _refreshing = refreshing;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Feed discovery runs on the server, which may fetch any site; its errors come back as messages.</summary>
public sealed class RemoteFeedFinder(ApiClient api) : IFeedFinder
{
    public Task<IReadOnlyList<FoundFeed>> FindAllAsync(string input, CancellationToken ct = default) =>
        api.SendAsync(HttpMethod.Post, "api/feeds/find", new FindRequest(input),
            ApiJsonContext.Default.FindRequest, ApiJsonContext.Default.IReadOnlyListFoundFeed, ct);
}
