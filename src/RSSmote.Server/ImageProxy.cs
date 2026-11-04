namespace RSSmote.Server;

/// <summary>
/// Fetches a favicon or thumbnail for the client, which may not fetch another origin itself (A19). Only http and
/// https, only images, at most <see cref="MaxBytes"/>; the browser caches the answer.
/// </summary>
public sealed class ImageProxy(HttpClient http)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public async Task<IResult> GetAsync(string? url, HttpResponse answer, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return Results.BadRequest();
        }

        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            var type = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode || type is null || !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || response.Content.Headers.ContentLength > MaxBytes)
            {
                return Results.NotFound();
            }

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > MaxBytes)
                {
                    return Results.NotFound();
                }

                buffer.Write(chunk, 0, read);
            }
            answer.Headers.CacheControl = "private, max-age=604800";
            return Results.Bytes(buffer.ToArray(), type);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            return Results.NotFound();
        }
    }
}
