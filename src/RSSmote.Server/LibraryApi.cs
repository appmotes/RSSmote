using System.Text;
using RSSmote.Core.Api;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Core.Opml;
using RSSmote.Data;

namespace RSSmote.Server;

/// <summary>The library over HTTP (A17). Every endpoint needs the sign-in cookie.</summary>
public static class LibraryApi
{
    public const string OpmlFileName = "RSSmote Feeds.opml";

    public static void MapLibrary(this RouteGroupBuilder api)
    {
        var library = api.MapGroup("").RequireAuthorization();

        library.MapGet("/library", (Store store, SettingsStore settings, Refresher refresher) => new LibrarySnapshot(
            store.Folders(),
            store.Feeds(),
            [.. store.Items(new ArticleStream.All(), StreamFilter.All, limit: int.MaxValue).Select(i => i with { Content = "" })],
            LibrarySettings.From(settings.Current),
            refresher.IsRefreshing));

        library.MapGet("/events", (LibraryEvents events, HttpContext http, CancellationToken ct) =>
        {
            // A proxy that buffers the answer would hold every event back.
            http.Response.Headers["X-Accel-Buffering"] = "no";
            return TypedResults.ServerSentEvents(events.Subscribe(ct));
        });

        library.MapGet("/items/{id:long}", (long id, Store store) =>
            store.Item(id) is { } item ? Results.Ok(item) : Results.NotFound());

        library.MapPost("/items/unread", (FlagRequest body, Store store) =>
        {
            store.SetUnread(body.Ids.ToList(), body.Value);
            return Results.NoContent();
        });

        library.MapPost("/items/starred", (FlagRequest body, Store store) =>
        {
            store.SetStarred(body.Ids.ToList(), body.Value);
            return Results.NoContent();
        });

        library.MapPost("/items/read-all", (ArticleStream stream, Store store) =>
        {
            store.MarkAllRead(stream);
            return Results.NoContent();
        });

        library.MapPost("/folders", (TitleRequest body, Store store) =>
            Results.Ok(store.FolderNamed(body.Title.Trim()) ?? store.AddFolder(body.Title.Trim())));

        library.MapPut("/folders/{id:long}", (long id, TitleRequest body, Store store) =>
        {
            store.RenameFolder(id, body.Title.Trim());
            return Results.NoContent();
        });

        library.MapDelete("/folders/{id:long}", (long id, bool? deleteFeeds, Store store) =>
        {
            store.DeleteFolder(id, deleteFeeds == true);
            return Results.NoContent();
        });

        library.MapPost("/feeds/find", async (FindRequest body, FeedFinder finder, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await finder.FindAllAsync(body.Input, ct));
            }
            catch (Exception e) when (e is FeedException or HttpRequestException or TaskCanceledException or IOException)
            {
                var message = e is TaskCanceledException ? "The server did not respond in time." : e.Message;
                return Results.UnprocessableEntity(new ErrorResponse(message));
            }
        });

        library.MapPost("/feeds", (AddFeedRequest body, Refresher refresher) => Results.Ok(refresher.AddFeed(body.Found, body.FolderId)));

        library.MapPut("/feeds/{id:long}", (long id, Feed feed, Store store) =>
        {
            store.UpdateFeed(feed with { Id = id });
            return Results.NoContent();
        });

        library.MapDelete("/feeds/{id:long}", (long id, Store store) =>
        {
            store.DeleteFeed(id);
            return Results.NoContent();
        });

        library.MapPost("/opml", (ImportRequest body, Store store) => new CountResponse(store.ImportOpml(body.Outlines)));

        library.MapGet("/opml", (Store store) =>
            Results.File(Encoding.UTF8.GetBytes(OpmlDocument.Export(store.Folders(), store.Feeds())), "text/x-opml", OpmlFileName));

        library.MapPost("/refresh", (Refresher refresher) =>
        {
            _ = refresher.RefreshAllAsync();
            return Results.Accepted();
        });

        library.MapGet("/settings", (SettingsStore settings) => LibrarySettings.From(settings.Current));

        library.MapPut("/settings", (LibrarySettings body, SettingsStore settings) =>
        {
            // The server stamps LastRefresh; a client never sets it.
            var updated = settings.Update(s => (body with { LastRefresh = s.LastRefresh }).ApplyTo(s));
            return LibrarySettings.From(updated);
        });

        library.MapGet("/image", (string? url, ImageProxy proxy, HttpContext http, CancellationToken ct) =>
            proxy.GetAsync(url, http.Response, ct));
    }
}
