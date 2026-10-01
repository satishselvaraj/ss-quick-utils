using Microsoft.Extensions.Logging;
using Raven.Core.Agents;
using Raven.Core.Auth;
using Raven.Core.Configuration;
using Raven.Core.Logging;
using Raven.Core.Models.GitLab;
using Raven.Core.Models.Rally;
using Raven.Scout;

namespace Raven.Forge;

/// <summary>
/// RAVEN Forge Agent - Git operations engine.
/// Creates branches, commits, and merge requests following RAVEN naming conventions.
/// Authenticates via OAuth Bearer token (no PAT required).
/// </summary>
public sealed class ForgeAgent : IAgent, IDisposable
{
    public string Name => "forge";
    public string Description => "Git operations - branches, commits, and merge requests";

    private readonly GitLabApiClient _gitlabClient;
    private readonly PreferencesConfig _preferences;
    private readonly ILogger<ForgeAgent> _logger;

    public ForgeAgent(GitLabConfig gitlabConfig, PreferencesConfig preferences,
        GitLabOAuthService oauthService, ILogger<ForgeAgent> logger)
    {
        _preferences = preferences;
        _logger = logger;
        _gitlabClient = new GitLabApiClient(gitlabConfig, logger.CreateChildLogger<GitLabApiClient>(), oauthService);
    }

    public Task<AgentHealthCheck> CheckHealthAsync(CancellationToken ct = default)
    {
        // Forge relies on the same GitLab connection as Scout
        return Task.FromResult(new AgentHealthCheck(Name, true, "Forge agent ready"));
    }

    /// <summary>
    /// Creates a feature or bugfix branch following RAVEN naming conventions.
    /// Pattern: feature/US12345-customer-api or bugfix/DE54321-auth-fix
    /// </summary>
    public async Task<AgentResult<GitLabBranch>> CreateBranchAsync(
        int projectId, RallyWorkItem workItem, string? baseBranch = null, CancellationToken ct = default)
    {
        try
        {
            var branchName = GenerateBranchName(workItem);
            var refBranch = baseBranch ?? _preferences.DefaultBranch;

            _logger.LogInformation("Forge: Creating branch {Branch} from {Ref} in project {Id}",
                branchName, refBranch, projectId);

            var branch = await _gitlabClient.CreateBranchAsync(projectId, branchName, refBranch, ct);

            if (branch is null)
                return AgentResult<GitLabBranch>.Fail($"Failed to create branch '{branchName}'", Name);

            _logger.LogInformation("Forge: Branch '{Branch}' created successfully", branchName);
            return AgentResult<GitLabBranch>.Ok(branch, Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forge: Branch creation failed");
            return AgentResult<GitLabBranch>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Creates a merge request for a work item's branch.
    /// </summary>
    public async Task<AgentResult<GitLabMergeRequest>> CreateMergeRequestAsync(
        int projectId, RallyWorkItem workItem, string sourceBranch,
        string? targetBranch = null, CancellationToken ct = default)
    {
        try
        {
            var target = targetBranch ?? _preferences.DefaultBranch;
            var title = GenerateMrTitle(workItem);
            var description = GenerateMrDescription(workItem);

            _logger.LogInformation("Forge: Creating MR '{Title}' ({Source} -> {Target})",
                title, sourceBranch, target);

            var mr = await _gitlabClient.CreateMergeRequestAsync(
                projectId, sourceBranch, target, title, description, ct);

            if (mr is null)
                return AgentResult<GitLabMergeRequest>.Fail("Failed to create merge request", Name);

            _logger.LogInformation("Forge: MR !{Iid} created: {Url}", mr.Iid, mr.WebUrl);
            return AgentResult<GitLabMergeRequest>.Ok(mr, Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forge: MR creation failed");
            return AgentResult<GitLabMergeRequest>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Lists open merge requests for a project.
    /// </summary>
    public async Task<AgentResult<List<GitLabMergeRequest>>> ListOpenMergeRequestsAsync(
        int projectId, CancellationToken ct = default)
    {
        try
        {
            var mrs = await _gitlabClient.ListMergeRequestsAsync(projectId, ct: ct);
            return AgentResult<List<GitLabMergeRequest>>.Ok(mrs, Name);
        }
        catch (Exception ex)
        {
            return AgentResult<List<GitLabMergeRequest>>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Generates a branch name following RAVEN conventions.
    /// Feature: feature/US12345-customer-api
    /// Bugfix:  bugfix/DE54321-auth-fix
    /// </summary>
    public string GenerateBranchName(RallyWorkItem workItem)
    {
        var prefix = workItem.Type == RallyWorkItemType.Defect
            ? _preferences.BranchPrefix.Bugfix
            : _preferences.BranchPrefix.Feature;

        var slug = Slugify(workItem.Name);
        return $"{prefix}/{workItem.FormattedId}-{slug}";
    }

    /// <summary>
    /// Generates a commit message following RAVEN conventions.
    /// Pattern: US12345 - Implement Customer API
    /// </summary>
    public static string GenerateCommitMessage(RallyWorkItem workItem, string action = "Implement")
    {
        return $"{workItem.FormattedId} - {action} {workItem.Name}";
    }

    private static string GenerateMrTitle(RallyWorkItem workItem)
    {
        return $"{workItem.FormattedId} - {workItem.Name}";
    }

    private static string GenerateMrDescription(RallyWorkItem workItem)
    {
        var type = workItem.Type == RallyWorkItemType.Defect ? "Defect" : "User Story";
        return $"""
                ## {type}: {workItem.FormattedId}

                **Title:** {workItem.Name}

                **Estimate:** {workItem.PlanEstimate?.ToString("F1") ?? "N/A"} points

                ---

                ### Description

                {workItem.Description}

                ---

                *Created by RAVEN - Rally-Aware AI Virtual Engineering Navigator*
                """;
    }

    private static string Slugify(string text)
    {
        return text
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace("_", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Aggregate("", (current, c) => current + c)
            .Trim('-');
    }

    public void Dispose() => _gitlabClient.Dispose();
}
