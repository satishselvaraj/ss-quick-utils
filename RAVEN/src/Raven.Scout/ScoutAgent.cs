using Microsoft.Extensions.Logging;
using Raven.Core.Agents;
using Raven.Core.Auth;
using Raven.Core.Configuration;
using Raven.Core.Logging;
using Raven.Core.Models.GitLab;

namespace Raven.Scout;

/// <summary>
/// RAVEN Scout Agent - Repository intelligence and discovery.
/// Maps repositories, discovers architecture, and performs impact analysis.
/// Authenticates via OAuth Bearer token (no PAT required).
/// </summary>
public sealed class ScoutAgent : IAgent, IDisposable
{
    public string Name => "scout";
    public string Description => "Repository intelligence - discovers projects, maps files, analyzes impact";

    private readonly GitLabApiClient _client;
    private readonly GitLabConfig _config;
    private readonly ILogger<ScoutAgent> _logger;

    public ScoutAgent(GitLabConfig config, GitLabOAuthService oauthService, ILogger<ScoutAgent> logger)
    {
        _config = config;
        _logger = logger;
        _client = new GitLabApiClient(config, logger.CreateChildLogger<GitLabApiClient>(), oauthService);
    }

    public async Task<AgentHealthCheck> CheckHealthAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_config.ClientId) || string.IsNullOrEmpty(_config.ClientSecret))
        {
            return new AgentHealthCheck(Name, false,
                "GitLab OAuth credentials not found in ~/.gitconfig. Add gitLabDevClientId/gitLabDevClientSecret.");
        }

        try
        {
            // Ensure we have a valid OAuth token (auto-authorizes if needed)
            var authResult = await _client.EnsureAuthenticatedAsync(ct);
            if (!authResult.Success)
            {
                return new AgentHealthCheck(Name, false, $"GitLab auth failed: {authResult.Error}");
            }

            var valid = await _client.ValidateConnectionAsync(ct);
            return valid
                ? new AgentHealthCheck(Name, true, $"Connected to {_config.Url} (OAuth Bearer)")
                : new AgentHealthCheck(Name, false, "GitLab authentication failed. Try: raven init");
        }
        catch (Exception ex)
        {
            return new AgentHealthCheck(Name, false, $"GitLab connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Discovers projects matching a search term or within the default group.
    /// </summary>
    public async Task<AgentResult<List<GitLabProject>>> DiscoverProjectsAsync(
        string? search = null, CancellationToken ct = default)
    {
        try
        {
            List<GitLabProject> projects;

            if (!string.IsNullOrEmpty(search))
            {
                projects = await _client.SearchProjectsAsync(search, ct: ct);
            }
            else if (!string.IsNullOrEmpty(_config.DefaultGroup))
            {
                projects = await _client.ListGroupProjectsAsync(_config.DefaultGroup, ct);
            }
            else
            {
                projects = await _client.SearchProjectsAsync(ct: ct);
            }

            _logger.LogInformation("Scout: Discovered {Count} projects", projects.Count);
            return AgentResult<List<GitLabProject>>.Ok(projects, Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scout: Project discovery failed");
            return AgentResult<List<GitLabProject>>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Gets a specific project by path or ID.
    /// </summary>
    public async Task<AgentResult<GitLabProject>> GetProjectAsync(
        string pathOrId, CancellationToken ct = default)
    {
        try
        {
            GitLabProject? project;

            if (int.TryParse(pathOrId, out var id))
            {
                project = await _client.GetProjectByIdAsync(id, ct);
            }
            else
            {
                project = await _client.GetProjectAsync(pathOrId, ct);
            }

            if (project is null)
                return AgentResult<GitLabProject>.Fail($"Project '{pathOrId}' not found", Name);

            return AgentResult<GitLabProject>.Ok(project, Name);
        }
        catch (Exception ex)
        {
            return AgentResult<GitLabProject>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Maps the repository structure (files and directories).
    /// </summary>
    public async Task<AgentResult<List<GitLabTreeItem>>> MapRepositoryAsync(
        int projectId, string? path = null, string? refName = null,
        bool recursive = false, CancellationToken ct = default)
    {
        try
        {
            var tree = await _client.GetRepositoryTreeAsync(projectId, path, refName, recursive, ct);
            _logger.LogInformation("Scout: Mapped {Count} items in project {Id}", tree.Count, projectId);
            return AgentResult<List<GitLabTreeItem>>.Ok(tree, Name);
        }
        catch (Exception ex)
        {
            return AgentResult<List<GitLabTreeItem>>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Performs basic impact analysis by searching for files matching patterns
    /// derived from a work item's description (e.g., class names, service names).
    /// </summary>
    public async Task<AgentResult<List<string>>> AnalyzeImpactAsync(
        int projectId, IEnumerable<string> searchTerms, CancellationToken ct = default)
    {
        try
        {
            var tree = await _client.GetRepositoryTreeAsync(projectId, recursive: true, ct: ct);
            var affectedFiles = new HashSet<string>();

            foreach (var term in searchTerms)
            {
                var matches = tree
                    .Where(t => t.IsFile && t.Path.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.Path);

                foreach (var match in matches)
                {
                    affectedFiles.Add(match);
                }
            }

            var result = affectedFiles.OrderBy(f => f).ToList();
            _logger.LogInformation("Scout: Impact analysis found {Count} affected files", result.Count);
            return AgentResult<List<string>>.Ok(result, Name);
        }
        catch (Exception ex)
        {
            return AgentResult<List<string>>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Gets the content of a specific file from the repository.
    /// </summary>
    public async Task<AgentResult<string>> GetFileContentAsync(
        int projectId, string filePath, string? refName = null, CancellationToken ct = default)
    {
        try
        {
            var content = await _client.GetFileContentAsync(projectId, filePath, refName, ct);
            if (content is null)
                return AgentResult<string>.Fail($"File '{filePath}' not found", Name);

            return AgentResult<string>.Ok(content, Name);
        }
        catch (Exception ex)
        {
            return AgentResult<string>.Fail(ex.Message, Name);
        }
    }

    public void Dispose() => _client.Dispose();
}


