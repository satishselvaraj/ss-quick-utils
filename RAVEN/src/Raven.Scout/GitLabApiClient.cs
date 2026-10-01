using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Raven.Core.Auth;
using Raven.Core.Configuration;
using Raven.Core.Models.GitLab;

namespace Raven.Scout;

/// <summary>
/// Low-level HTTP client for the GitLab REST API v4.
/// Authenticates via OAuth Bearer token (obtained from clientId/clientSecret).
/// No PAT required.
/// </summary>
public sealed class GitLabApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<GitLabApiClient> _logger;
    private readonly string _baseUrl;
    private readonly GitLabOAuthService? _oauthService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitLabApiClient(GitLabConfig config, ILogger<GitLabApiClient> logger,
        GitLabOAuthService? oauthService = null)
    {
        _logger = logger;
        _baseUrl = config.Url.TrimEnd('/') + "/api/v4";
        _oauthService = oauthService;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Configure auth: prefer OAuth Bearer token, fall back to PAT if available
        if (_oauthService is not null && _oauthService.HasToken)
        {
            _oauthService.ConfigureHttpClient(_http);
        }
        else if (!string.IsNullOrEmpty(config.PersonalAccessToken))
        {
            _http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", config.PersonalAccessToken);
        }
    }

    /// <summary>
    /// Ensures the OAuth token is valid before making API calls.
    /// Called automatically by agents before first use.
    /// </summary>
    public async Task<(bool Success, string? Error)> EnsureAuthenticatedAsync(CancellationToken ct = default)
    {
        if (_oauthService is null)
        {
            // No OAuth service - check if PAT is set
            if (_http.DefaultRequestHeaders.Contains("PRIVATE-TOKEN"))
                return (true, null);
            return (false, "No authentication configured");
        }

        var result = await _oauthService.EnsureTokenAsync(ct);
        if (result.Success)
        {
            _oauthService.ConfigureHttpClient(_http);
        }
        return result;
    }

    /// <summary>
    /// Lists projects accessible to the authenticated user, optionally filtered by search term.
    /// </summary>
    public async Task<List<GitLabProject>> SearchProjectsAsync(
        string? search = null, int perPage = 20, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/projects?membership=true&per_page={perPage}&order_by=last_activity_at";
        if (!string.IsNullOrEmpty(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        _logger.LogDebug("GitLab API: GET {Url}", url);
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<GitLabProject>>(JsonOptions, ct) ?? [];
    }

    /// <summary>
    /// Gets a project by its path (e.g., "group/subgroup/project").
    /// </summary>
    public async Task<GitLabProject?> GetProjectAsync(string pathWithNamespace, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(pathWithNamespace);
        var url = $"{_baseUrl}/projects/{encoded}";

        _logger.LogDebug("GitLab API: GET {Url}", url);
        var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<GitLabProject>(JsonOptions, ct);
    }

    /// <summary>
    /// Gets a project by its numeric ID.
    /// </summary>
    public async Task<GitLabProject?> GetProjectByIdAsync(int projectId, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/projects/{projectId}";

        _logger.LogDebug("GitLab API: GET {Url}", url);
        var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<GitLabProject>(JsonOptions, ct);
    }

    /// <summary>
    /// Lists the repository tree (files and directories) for a project.
    /// </summary>
    public async Task<List<GitLabTreeItem>> GetRepositoryTreeAsync(
        int projectId, string? path = null, string? refName = null,
        bool recursive = false, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/projects/{projectId}/repository/tree?per_page=100";
        if (!string.IsNullOrEmpty(path)) url += $"&path={Uri.EscapeDataString(path)}";
        if (!string.IsNullOrEmpty(refName)) url += $"&ref={Uri.EscapeDataString(refName)}";
        if (recursive) url += "&recursive=true";

        _logger.LogDebug("GitLab API: GET {Url}", url);
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<GitLabTreeItem>>(JsonOptions, ct) ?? [];
    }

    /// <summary>
    /// Gets the content of a file from the repository.
    /// </summary>
    public async Task<string?> GetFileContentAsync(
        int projectId, string filePath, string? refName = null, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(filePath);
        var url = $"{_baseUrl}/projects/{projectId}/repository/files/{encoded}/raw";
        if (!string.IsNullOrEmpty(refName)) url += $"?ref={Uri.EscapeDataString(refName)}";

        _logger.LogDebug("GitLab API: GET {Url}", url);
        var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// Creates a new branch in a project.
    /// </summary>
    public async Task<GitLabBranch?> CreateBranchAsync(
        int projectId, string branchName, string refBranch, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/projects/{projectId}/repository/branches";
        var payload = new { branch = branchName, @ref = refBranch };

        _logger.LogDebug("GitLab API: POST {Url} - branch={Branch} ref={Ref}", url, branchName, refBranch);
        var response = await _http.PostAsJsonAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("GitLab API: Branch creation failed: {Error}", error);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GitLabBranch>(JsonOptions, ct);
    }

    /// <summary>
    /// Creates a merge request.
    /// </summary>
    public async Task<GitLabMergeRequest?> CreateMergeRequestAsync(
        int projectId, string sourceBranch, string targetBranch,
        string title, string? description = null, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/projects/{projectId}/merge_requests";
        var payload = new
        {
            source_branch = sourceBranch,
            target_branch = targetBranch,
            title,
            description = description ?? "",
            remove_source_branch = true
        };

        _logger.LogDebug("GitLab API: POST {Url} - {Source} -> {Target}", url, sourceBranch, targetBranch);
        var response = await _http.PostAsJsonAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("GitLab API: MR creation failed: {Error}", error);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GitLabMergeRequest>(JsonOptions, ct);
    }

    /// <summary>
    /// Lists open merge requests for a project.
    /// </summary>
    public async Task<List<GitLabMergeRequest>> ListMergeRequestsAsync(
        int projectId, string state = "opened", CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/projects/{projectId}/merge_requests?state={state}&per_page=50";

        _logger.LogDebug("GitLab API: GET {Url}", url);
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<GitLabMergeRequest>>(JsonOptions, ct) ?? [];
    }

    /// <summary>
    /// Lists projects within a group.
    /// </summary>
    public async Task<List<GitLabProject>> ListGroupProjectsAsync(
        string groupPath, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(groupPath);
        var url = $"{_baseUrl}/groups/{encoded}/projects?per_page=100&include_subgroups=true&order_by=last_activity_at";

        _logger.LogDebug("GitLab API: GET {Url}", url);
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<GitLabProject>>(JsonOptions, ct) ?? [];
    }

    /// <summary>
    /// Validates the connection by fetching the current user.
    /// </summary>
    public async Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/user";
        var response = await _http.GetAsync(url, ct);
        return response.IsSuccessStatusCode;
    }

    public void Dispose() => _http.Dispose();
}
