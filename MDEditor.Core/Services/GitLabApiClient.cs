using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MDEditor.Core.Services;

/// <summary>A GitLab API call that failed; <see cref="ApiMessage"/> is the server's own explanation when it gave one.</summary>
public class GitLabApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ApiMessage { get; }

    public GitLabApiException(HttpStatusCode statusCode, string? apiMessage)
        : base($"GitLab API error {(int)statusCode} {statusCode}" + (string.IsNullOrWhiteSpace(apiMessage) ? "" : $": {apiMessage}"))
    {
        StatusCode = statusCode;
        ApiMessage = apiMessage;
    }
}

/// <summary>
/// Thin JSON client for one GitLab server's REST API v4, authenticated with a personal access token
/// (<c>PRIVATE-TOKEN</c> header). Works against gitlab.com and self-hosted instances on any port.
/// </summary>
public sealed class GitLabApiClient
{
    private const int DefaultPageSize = 100;

    private readonly HttpClient _http;
    private readonly string? _token;

    /// <summary>Normalized server root (see <see cref="NormalizeServerUrl"/>).</summary>
    public string ServerUrl { get; }

    public GitLabApiClient(HttpClient http, string serverUrl, string? token)
    {
        _http = http;
        _token = string.IsNullOrWhiteSpace(token) ? null : token;
        ServerUrl = NormalizeServerUrl(serverUrl);
    }

    /// <summary>
    /// Turns what a user typed into a server root: adds https:// when no scheme is given, and strips a trailing
    /// '/', "/api/v4" and "/users/sign_in". Keeps host, port and any sub-path (relative URL root installs).
    /// </summary>
    /// <exception cref="FormatException">Not an http(s) URL.</exception>
    public static string NormalizeServerUrl(string? input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new FormatException("Server URL is empty.");

        if (!text.Contains("://"))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || string.IsNullOrEmpty(uri.Host))
            throw new FormatException($"'{input}' is not a valid http(s) server URL.");

        var path = uri.AbsolutePath.TrimEnd('/');
        foreach (var suffix in new[] { "/users/sign_in", "/api/v4" })
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                path = path[..^suffix.Length].TrimEnd('/');
        }

        return $"{uri.Scheme}://{uri.Authority}{path}";
    }

    /// <summary>GET a single JSON document, e.g. <c>"user"</c> or <c>"projects/42"</c>.</summary>
    public async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        var (body, _) = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        return body;
    }

    /// <summary>GET a raw (non-JSON) body such as a file's raw content.</summary>
    public async Task<string> GetRawAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, path, null);
        using var response = await _http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new GitLabApiException(response.StatusCode, ExtractMessage(text));
        return text;
    }

    /// <summary>GET every page of a list endpoint (follows <c>X-Next-Page</c>), up to <paramref name="maxPages"/> pages.</summary>
    /// <returns>The items and whether more pages existed beyond the cap.</returns>
    public async Task<(List<JsonElement> Items, bool Truncated)> GetAllPagesAsync(string path, int maxPages = 20, CancellationToken cancellationToken = default)
    {
        var items = new List<JsonElement>();
        var separator = path.Contains('?') ? '&' : '?';
        string? page = "1";
        var pagesRead = 0;

        while (!string.IsNullOrEmpty(page) && pagesRead < maxPages)
        {
            var (body, next) = await SendAsync(HttpMethod.Get, $"{path}{separator}per_page={DefaultPageSize}&page={page}", null, cancellationToken);
            if (body.ValueKind == JsonValueKind.Array)
                items.AddRange(body.EnumerateArray());
            page = next;
            pagesRead++;
        }

        return (items, !string.IsNullOrEmpty(page));
    }

    public async Task<JsonElement> PostAsync(string path, object body, CancellationToken cancellationToken = default)
    {
        var (response, _) = await SendAsync(HttpMethod.Post, path, body, cancellationToken);
        return response;
    }

    private async Task<(JsonElement Body, string? NextPage)> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body);
        using var response = await _http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new GitLabApiException(response.StatusCode, ExtractMessage(text));

        JsonElement json;
        try
        {
            using var doc = JsonDocument.Parse(text);
            json = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // A reverse proxy or a non-GitLab site answered with HTML: report it as "not a GitLab API".
            throw new GitLabApiException(HttpStatusCode.NotFound, "The server did not return JSON; is this a GitLab URL?");
        }

        string? next = null;
        if (response.Headers.TryGetValues("X-Next-Page", out var values))
            next = values.FirstOrDefault();

        return (json, string.IsNullOrWhiteSpace(next) ? null : next);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, $"{ServerUrl}/api/v4/{path.TrimStart('/')}");
        if (_token != null)
            request.Headers.Add("PRIVATE-TOKEN", _token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return request;
    }

    /// <summary>Pulls the human-readable part out of GitLab's error bodies ({"message": ...}, {"error": ...}).</summary>
    public static string? ExtractMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "message", "error", "error_description" })
                {
                    if (doc.RootElement.TryGetProperty(name, out var value))
                    {
                        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON (HTML error page): fall through and return nothing useful.
        }

        return null;
    }

    /// <summary>Escapes a repository path for use as a single URL segment ("docs/a b.md" -> "docs%2Fa%20b.md").</summary>
    public static string EscapeFilePath(string path) => Uri.EscapeDataString(path);
}
