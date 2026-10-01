using System.CommandLine;
using Raven.Core.Configuration;

namespace Raven.Cli.Commands;

/// <summary>
/// raven init - Interactive setup wizard that creates ~/.raven/config.json.
/// Auto-detects credentials from ~/.gitconfig before prompting.
/// </summary>
internal static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Initialize RAVEN configuration (reads ~/.gitconfig, creates ~/.raven/config.json)");

        command.SetHandler(async () =>
        {
            ConsoleHelper.WriteBanner();
            ConsoleHelper.WriteHeader("RAVEN Setup");

            var config = new RavenConfig();

            // ── Step 1: Auto-detect from ~/.gitconfig ──────────────────────
            Console.WriteLine();
            ConsoleHelper.WriteInfo($"Scanning ~/.gitconfig ({GitConfigService.ConfigPath})...");

            var gitCreds = GitConfigService.ReadAll();
            var gitLabFound = gitCreds.GitLab is not null;
            var rallyFound = gitCreds.Rally is not null;

            if (gitLabFound)
            {
                var gl = gitCreds.GitLab!;
                config.GitLab.Url = gl.Url;
                config.GitLab.ClientId = gl.ClientId ?? "";
                config.GitLab.ClientSecret = gl.ClientSecret ?? "";

                ConsoleHelper.WriteSuccess($"GitLab URL:       {gl.Url}");
                ConsoleHelper.WriteSuccess($"GitLab Client ID: {Mask(gl.ClientId, 12)}");
                ConsoleHelper.WriteSuccess($"GitLab Secret:    {Mask(gl.ClientSecret, 8)}");
            }
            else
            {
                ConsoleHelper.WriteWarning("No GitLab credentials found in ~/.gitconfig");
            }

            if (rallyFound)
            {
                var rl = gitCreds.Rally!;
                config.Rally.Url = rl.Url;
                config.Rally.ApiKey = rl.ApiKey ?? "";

                if (!string.IsNullOrEmpty(rl.Workspace))
                    config.Rally.Workspace = rl.Workspace;
                if (!string.IsNullOrEmpty(rl.Project))
                    config.Rally.Project = rl.Project;

                ConsoleHelper.WriteSuccess($"Rally URL:        {rl.Url}");
                ConsoleHelper.WriteSuccess($"Rally API Key:    {Mask(rl.ApiKey, 8)}");

                if (!string.IsNullOrEmpty(rl.Workspace))
                    ConsoleHelper.WriteSuccess($"Rally Workspace:  {rl.Workspace}");
                if (!string.IsNullOrEmpty(rl.Project))
                    ConsoleHelper.WriteSuccess($"Rally Project:    {rl.Project}");
            }
            else
            {
                ConsoleHelper.WriteWarning("No Rally credentials found in ~/.gitconfig");
            }

            // ── Step 2: Prompt for anything not found in .gitconfig ────────
            Console.WriteLine();
            ConsoleHelper.WriteInfo("Review and complete configuration (press Enter to keep detected values):");

            // Rally
            Console.WriteLine();
            ConsoleHelper.WriteHeader("Rally (Broadcom Agile Central)");

            if (!rallyFound)
            {
                config.Rally.Url = ConsoleHelper.Prompt("Rally URL", config.Rally.Url);
                config.Rally.ApiKey = ConsoleHelper.PromptSecret("Rally API Key (_xxxxxxxx)");
            }

            // Always prompt for workspace/project if not set (these aren't secrets)
            if (string.IsNullOrEmpty(config.Rally.Workspace))
                config.Rally.Workspace = ConsoleHelper.Prompt("Rally Workspace path (e.g., /workspace/12345)");

            if (string.IsNullOrEmpty(config.Rally.Project))
                config.Rally.Project = ConsoleHelper.Prompt("Rally Project path (e.g., /project/67890)");

            // GitLab
            Console.WriteLine();
            ConsoleHelper.WriteHeader("GitLab");

            if (!gitLabFound)
            {
                config.GitLab.Url = ConsoleHelper.Prompt("GitLab URL", config.GitLab.Url);
                ConsoleHelper.WriteWarning("OAuth Client ID/Secret not found in ~/.gitconfig.");
                ConsoleHelper.WriteInfo("Add [credential \"https://your-gitlab-url\"] with gitLabDevClientId and gitLabDevClientSecret.");
            }
            else
            {
                ConsoleHelper.WriteInfo("GitLab API calls will use OAuth Bearer token (auto-authorized via browser).");
                ConsoleHelper.WriteInfo("No PAT required.");
            }

            config.GitLab.DefaultGroup = ConsoleHelper.Prompt("Default GitLab group (optional)", config.GitLab.DefaultGroup);

            // Preferences
            Console.WriteLine();
            ConsoleHelper.WriteHeader("Preferences");
            config.Preferences.DefaultBranch = ConsoleHelper.Prompt("Default branch", config.Preferences.DefaultBranch);
            var autoUpdate = ConsoleHelper.Prompt("Auto-update Rally on MR creation? (y/n)", "y");
            config.Preferences.AutoUpdateRally = autoUpdate.Equals("y", StringComparison.OrdinalIgnoreCase);

            // ── Step 3: Save (secrets from .gitconfig are NOT written to config.json) ──
            // Clear secrets before saving - they live in .gitconfig, not config.json
            var savedConfig = new RavenConfig
            {
                Rally = new RallyConfig
                {
                    Url = config.Rally.Url,
                    // ApiKey intentionally omitted - read from .gitconfig at runtime
                    Workspace = config.Rally.Workspace,
                    Project = config.Rally.Project
                },
                GitLab = new GitLabConfig
                {
                    Url = config.GitLab.Url,
                    // No PAT - OAuth Bearer token used instead (clientId/clientSecret from .gitconfig)
                    DefaultGroup = config.GitLab.DefaultGroup
                },
                Preferences = config.Preferences
            };

            // If Rally API key was manually entered (not from .gitconfig), store it
            if (!rallyFound && !string.IsNullOrEmpty(config.Rally.ApiKey))
                savedConfig.Rally.ApiKey = config.Rally.ApiKey;

            await ConfigManager.SaveAsync(savedConfig);

            Console.WriteLine();
            ConsoleHelper.WriteSuccess($"Configuration saved to {ConfigManager.HomePath}");

            if (gitLabFound || rallyFound)
            {
                ConsoleHelper.WriteInfo("Secrets are read from ~/.gitconfig at runtime (not stored in config.json).");
            }

            ConsoleHelper.WriteInfo("Run 'raven status' to verify connections.");
            Console.WriteLine();
        });

        return command;
    }

    /// <summary>
    /// Masks a secret string, showing only the first N characters.
    /// </summary>
    private static string Mask(string? value, int showChars)
    {
        if (string.IsNullOrEmpty(value))
            return "(empty)";

        if (value.Length <= showChars)
            return new string('*', value.Length);

        return value[..showChars] + new string('*', Math.Min(value.Length - showChars, 20));
    }
}
