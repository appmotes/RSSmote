using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace RSSmote.Server;

/// <summary>
/// Serves the published browser app from a folder (A24). A file the publish compressed with brotli is sent
/// compressed. A file whose name carries its content hash never changes and is cached for good; everything else,
/// index.html and dotnet.js among it, is checked on every load, so a new deploy is picked up at once.
/// </summary>
public static partial class WebApp
{
    [GeneratedRegex(@"^/_framework/.+\.[a-z0-9]{10}\.[a-z]+$")]
    private static partial Regex Fingerprinted();

    private const string Compressed = ".br";

    public static void UseWebApp(this WebApplication app, string root)
    {
        var files = new PhysicalFileProvider(Path.GetFullPath(root));
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".wasm"] = "application/wasm";
        types.Mappings[".mjs"] = "text/javascript";
        types.Mappings[".map"] = "application/json";

        app.Use((http, next) =>
        {
            var path = http.Request.Path.Value ?? "/";
            if (path.StartsWith("/api/", StringComparison.Ordinal))
            {
                return next();
            }

            if (path == "/")
            {
                http.Request.Path = path = "/index.html";
            }

            http.Response.Headers.Vary = "Accept-Encoding";
            if (http.Request.Headers.AcceptEncoding.ToString().Contains("br", StringComparison.Ordinal)
                && files.GetFileInfo(path + Compressed).Exists)
            {
                http.Request.Path = path + Compressed;
                http.Response.Headers.ContentEncoding = "br";
            }
            return next();
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = new UncompressedTypes(types),
            OnPrepareResponse = c =>
            {
                var path = c.Context.Request.Path.Value!;
                if (path.EndsWith(Compressed, StringComparison.Ordinal))
                {
                    path = path[..^Compressed.Length];
                }

                c.Context.Response.Headers.CacheControl = Fingerprinted().IsMatch(path) ? "public, max-age=31536000, immutable" : "no-cache";
                c.Context.Response.Headers.XContentTypeOptions = "nosniff";
            },
        });
    }

    /// <summary>A compressed file has the type of the file it was made from.</summary>
    private sealed class UncompressedTypes(IContentTypeProvider types) : IContentTypeProvider
    {
        public bool TryGetContentType(string subpath, out string contentType) =>
            types.TryGetContentType(subpath.EndsWith(Compressed, StringComparison.Ordinal) ? subpath[..^Compressed.Length] : subpath,
                out contentType!);
    }
}
