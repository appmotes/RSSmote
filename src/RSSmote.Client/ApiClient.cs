using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using RSSmote.Core.Api;

namespace RSSmote.Client;

/// <summary>The server answered 401: the device is not signed in, or no longer is.</summary>
public sealed class SignedOutException() : Exception("Not signed in.");

/// <summary>
/// The server's API over one <see cref="HttpClient"/> whose base address is the server. Every request names this
/// app with <see cref="ClientId"/>; a 401 raises <see cref="SignedOut"/> and throws <see cref="SignedOutException"/>.
/// An error the server explains arrives as an <see cref="HttpRequestException"/> with its message.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    private static readonly HttpRequestOptionsKey<bool> StreamingResponse = new("WebAssemblyEnableStreamingResponse");

    public string ClientId { get; } = Guid.NewGuid().ToString("N");

    public event EventHandler? SignedOut;

    /// <summary>True when the key is right; false when it is not. Throws when the server refuses to check (429).</summary>
    public async Task<bool> SignInAsync(string key, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/login",
            JsonContent.Create(new LoginRequest(key), ApiJsonContext.Default.LoginRequest), ct, signedOutIsError: false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return false;
        await EnsureSuccessAsync(response, ct);
        return true;
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/logout", null, ct, signedOutIsError: false);
    }

    public async Task<bool> IsSignedInAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/session", null, ct, signedOutIsError: false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return false;
        await EnsureSuccessAsync(response, ct);
        return true;
    }

    public async Task<T> GetAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync(type, ct))!;
    }

    public async Task SendAsync<TBody>(HttpMethod method, string path, TBody body, JsonTypeInfo<TBody> bodyType, CancellationToken ct = default)
    {
        using var response = await SendAsync(method, path, JsonContent.Create(body, bodyType), ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task SendAsync(HttpMethod method, string path, CancellationToken ct = default)
    {
        using var response = await SendAsync(method, path, null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<TResult> SendAsync<TBody, TResult>(HttpMethod method, string path, TBody body, JsonTypeInfo<TBody> bodyType,
                                                         JsonTypeInfo<TResult> resultType, CancellationToken ct = default)
    {
        using var response = await SendAsync(method, path, JsonContent.Create(body, bodyType), ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync(resultType, ct))!;
    }

    /// <summary>The event stream, answered as soon as its headers arrive; the caller disposes it.</summary>
    public async Task<HttpResponseMessage> OpenEventsAsync(CancellationToken ct)
    {
        var request = Request(HttpMethod.Get, "api/events", null);
        // A browser buffers the whole answer unless told to stream it.
        request.Options.Set(StreamingResponse, true);
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        try
        {
            ThrowIfSignedOut(response);
            await EnsureSuccessAsync(response, ct);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct,
                                                      bool signedOutIsError = true)
    {
        var response = await http.SendAsync(Request(method, path, content), ct);
        if (signedOutIsError)
            ThrowIfSignedOut(response);
        return response;
    }

    private HttpRequestMessage Request(HttpMethod method, string path, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(ApiNames.ClientHeader, ClientId);
        return request;
    }

    private void ThrowIfSignedOut(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return;
        response.Dispose();
        SignedOut?.Invoke(this, EventArgs.Empty);
        throw new SignedOutException();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        string? message = null;
        try
        {
            if (response.Content.Headers.ContentType?.MediaType == "application/json")
                message = (await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.ErrorResponse, ct))?.Message;
        }
        catch (System.Text.Json.JsonException)
        {
        }
        message ??= response.StatusCode == HttpStatusCode.TooManyRequests
            ? "Too many attempts. Wait a minute and try again."
            : $"The server answered {(int)response.StatusCode}.";
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
