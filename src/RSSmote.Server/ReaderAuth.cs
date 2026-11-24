using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace RSSmote.Server;

/// <summary>
/// The Reader API's tokens (A25): the access key's stamp, protected with the server's data protection keys. A new key
/// changes the stamp, so it signs out every token as it does every cookie.
/// </summary>
public sealed class ReaderTokens(IDataProtectionProvider provider, AccessKey key)
{
    private readonly IDataProtector _protector = provider.CreateProtector("RSSmote.Reader");

    public string Issue() => _protector.Protect(key.Stamp);

    public bool IsValid(string token)
    {
        try
        {
            return _protector.Unprotect(token) == key.Stamp;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}

/// <summary>Signs a Reader API request in from its <c>Authorization</c> header; without one the request is anonymous.</summary>
public sealed class ReaderAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ReaderTokens tokens) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Reader";

    private static readonly string Prefix = ReaderIds.AuthScheme + " auth=";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!tokens.IsValid(header[Prefix.Length..].Trim()))
        {
            return Task.FromResult(AuthenticateResult.Fail("The token is not valid."));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity([], SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
