using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using RSSmote.Core.Api;

namespace RSSmote.Server;

/// <summary>
/// The one key that signs a device in, read from <c>RSSMOTE_KEY</c>. Only its hash is kept. <see cref="Stamp"/>
/// goes into the sign-in cookie, so a new key signs every device out.
/// </summary>
public sealed class AccessKey
{
    public const string Variable = "RSSMOTE_KEY";
    public const int MinimumLength = 12;

    private readonly byte[] _hash;

    public AccessKey(string key)
    {
        _hash = Hash(key);
        Stamp = Convert.ToHexString(SHA256.HashData(_hash))[..16];
    }

    public string Stamp { get; }

    public bool Matches(string? candidate) =>
        candidate is not null && CryptographicOperations.FixedTimeEquals(_hash, Hash(candidate));

    public static AccessKey From(IConfiguration configuration) =>
        configuration[Variable] is { Length: >= MinimumLength } key
            ? new AccessKey(key)
            : throw new InvalidOperationException($"Set {Variable} to a key of at least {MinimumLength} characters.");

    private static byte[] Hash(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));
}

/// <summary>Cookie sign-in with the access key (A17), and the endpoints that sign in and out.</summary>
public static class Access
{
    public const string LoginPolicy = "login";
    private const string StampClaim = "rssmote:key";

    public static void AddAccess(this IServiceCollection services, AccessKey key)
    {
        services.AddSingleton(key);
        services.AddAuthorization();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Cookie.Name = "rssmote";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.ExpireTimeSpan = TimeSpan.FromDays(365);
            o.SlidingExpiration = true;
            // An API answers 401; there is no sign-in page to redirect to.
            o.Events.OnRedirectToLogin = c =>
            {
                c.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = c =>
            {
                c.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            o.Events.OnValidatePrincipal = c =>
            {
                if (c.Principal?.FindFirst(StampClaim)?.Value != key.Stamp)
                    c.RejectPrincipal();
                return Task.CompletedTask;
            };
        });
    }

    public static void MapAccess(this RouteGroupBuilder api)
    {
        api.MapPost("/login", async (LoginRequest body, AccessKey key, HttpContext http) =>
        {
            if (!key.Matches(body.Key))
                return Results.Unauthorized();
            var identity = new ClaimsIdentity([new Claim(StampClaim, key.Stamp)], CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
            return Results.NoContent();
        }).RequireRateLimiting(LoginPolicy);

        api.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync();
            return Results.NoContent();
        });

        api.MapGet("/session", () => Results.NoContent()).RequireAuthorization();
    }
}
