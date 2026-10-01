using System.CommandLine;
using Raven.Core.Configuration;

namespace Raven.Cli.Commands;

/// <summary>
/// raven status - Shows agent health, connection status, and credential sources.
/// </summary>
internal static class StatusCommand
{
    public static Command Create()
    {
        var command = new Command("status", "Check RAVEN agent status and connections");

        command.SetHandler(async () =>
        {
            // Status works even without init - shows what .gitconfig provides
            var config = await ConfigManager.LoadAsync();

            // ── Credential Sources ─────────────────────────────────────
            ConsoleHelper.WriteHeader("Credential Sources");

            if (config.GitLab.LoadedFromGitConfig)
            {
                ConsoleHelper.WriteSuccess($"GitLab URL:       {config.GitLab.Url} (from ~/.gitconfig)");
                ConsoleHelper.WriteSuccess($"GitLab Client ID: {(string.IsNullOrEmpty(config.GitLab.ClientId) ? "not set" : "loaded")} (from ~/.gitconfig)");
                ConsoleHelper.WriteSuccess($"GitLab Secret:    {(string.IsNullOrEmpty(config.GitLab.ClientSecret) ? "not set" : "loaded")} (from ~/.gitconfig)");
            }
            else
            {
                ConsoleHelper.WriteWarning("GitLab: No credentials in ~/.gitconfig");
            }

            // Check for cached OAuth token
            var tokenCachePath = Path.Combine(ConfigManager.HomePath, "token.json");
            if (File.Exists(tokenCachePath))
                ConsoleHelper.WriteSuccess("GitLab OAuth:     token cached (in ~/.raven/token.json)");
            else
                ConsoleHelper.WriteInfo("GitLab OAuth:     will authorize on first API call (browser flow)");

            if (config.Rally.LoadedFromGitConfig)
            {
                ConsoleHelper.WriteSuccess($"Rally URL:        {config.Rally.Url} (from ~/.gitconfig)");
                ConsoleHelper.WriteSuccess($"Rally API Key:    {(string.IsNullOrEmpty(config.Rally.ApiKey) ? "not set" : "loaded")} (from ~/.gitconfig)");
            }
            else if (!string.IsNullOrEmpty(config.Rally.ApiKey))
            {
                ConsoleHelper.WriteSuccess($"Rally URL:        {config.Rally.Url} (from config.json)");
                ConsoleHelper.WriteSuccess("Rally API Key:    loaded (from config.json)");
            }
            else
            {
                ConsoleHelper.WriteWarning("Rally: No credentials configured");
            }

            if (!ConfigManager.IsInitialized())
            {
                Console.WriteLine();
                ConsoleHelper.WriteWarning("RAVEN not fully initialized. Run: raven init");
                ConsoleHelper.WriteInfo("(Credentials from ~/.gitconfig are available but workspace/project may be missing)");
                Console.WriteLine();
                return;
            }

            // ── Agent Health ───────────────────────────────────────────
            ConsoleHelper.WriteHeader("Agent Status");

            var (provider, orchestrator) = await Program.BuildServicesAsync();
            await using var _ = provider;

            var agents = orchestrator.ListAgents();
            ConsoleHelper.WriteInfo($"Registered agents: {agents.Count}");
            Console.WriteLine();

            var checks = await orchestrator.CheckAllAgentsAsync();

            foreach (var check in checks)
            {
                if (check.IsHealthy)
                {
                    ConsoleHelper.WriteSuccess($"{check.AgentName.ToUpperInvariant()}: {check.Message}");
                }
                else
                {
                    ConsoleHelper.WriteError($"{check.AgentName.ToUpperInvariant()}: {check.Message}");
                }
            }

            Console.WriteLine();

            var healthy = checks.Count(c => c.IsHealthy);
            if (healthy == checks.Count)
            {
                ConsoleHelper.WriteSuccess("All agents operational.");
            }
            else
            {
                ConsoleHelper.WriteWarning($"{healthy}/{checks.Count} agents healthy.");
            }

            Console.WriteLine();
        });

        return command;
    }
}
