using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Raven.Cli.Commands;
using Raven.Core.Auth;
using Raven.Core.Configuration;
using Raven.Core.Orchestrator;
using Raven.Forge;
using Raven.Planner;
using Raven.Relay;
using Raven.Scout;

namespace Raven.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // If args provided, run as single command (CI/CD, scripts, aliases)
        if (args.Length > 0)
        {
            return await BuildRootCommand().InvokeAsync(args);
        }

        // No args: launch interactive REPL shell
        return await RunInteractiveShellAsync();
    }

    /// <summary>
    /// Interactive REPL shell. Shows banner, then loops on raven> prompt.
    /// Type commands directly: work, story US12345, branch US12345 my-project, etc.
    /// </summary>
    private static async Task<int> RunInteractiveShellAsync()
    {
        ConsoleHelper.WriteBanner();

        // Show quick credential status on startup
        var config = await ConfigManager.LoadAsync();
        var gitlabOk = config.GitLab.LoadedFromGitConfig && !string.IsNullOrEmpty(config.GitLab.ClientId);
        var rallyOk = !string.IsNullOrEmpty(config.Rally.ApiKey);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  GitLab: ");
        Console.ForegroundColor = gitlabOk ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.Write(gitlabOk ? "✓ " : "⚠ ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(config.GitLab.Url);

        Console.Write("  |  Rally: ");
        Console.ForegroundColor = rallyOk ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.Write(rallyOk ? "✓ " : "⚠ ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(config.Rally.Url);
        Console.ResetColor();

        if (!ConfigManager.IsInitialized())
        {
            ConsoleHelper.WriteWarning("Not initialized. Type 'init' to set up RAVEN.");
        }

        Console.WriteLine();
        ConsoleHelper.WriteInfo("Type a command, 'help' for available commands, or 'exit' to quit.");
        Console.WriteLine();

        var rootCommand = BuildRootCommand();

        // REPL loop
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  raven> ");
            Console.ResetColor();

            var line = Console.ReadLine();

            // Ctrl+C or closed stream
            if (line is null)
                break;

            var input = line.Trim();

            // Skip empty lines
            if (string.IsNullOrEmpty(input))
                continue;

            // Built-in shell commands
            switch (input.ToLowerInvariant())
            {
                case "exit" or "quit" or "q":
                    ConsoleHelper.WriteInfo("Goodbye!");
                    return 0;

                case "clear" or "cls":
                    Console.Clear();
                    continue;

                case "help" or "?":
                    WriteShellHelp();
                    continue;

                case "banner":
                    ConsoleHelper.WriteBanner();
                    continue;
            }

            // Parse input into args (respects quoted strings)
            var shellArgs = ParseShellArgs(input);

            try
            {
                await rootCommand.InvokeAsync(shellArgs);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Command failed: {ex.Message}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Builds the root command with all subcommands registered.
    /// </summary>
    private static RootCommand BuildRootCommand()
    {
        return new RootCommand("RAVEN - Rally-Aware AI Virtual Engineering Navigator")
        {
            InitCommand.Create(),
            StatusCommand.Create(),
            WorkCommand.Create(),
            StoryCommand.Create(),
            SprintCommand.Create(),
            ProjectCommand.Create(),
            BranchCommand.Create(),
            MrCommand.Create(),
            PlanCommand.Create()
        };
    }

    private static void WriteShellHelp()
    {
        ConsoleHelper.WriteHeader("RAVEN Commands");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Setup");
        Console.ResetColor();
        Console.WriteLine("    init                                  Initialize RAVEN (reads ~/.gitconfig)");
        Console.WriteLine("    status                                Check agent health and connections");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Rally");
        Console.ResetColor();
        Console.WriteLine("    work                                  List your stories/defects (current sprint)");
        Console.WriteLine("    story <id>                            View a Rally work item");
        Console.WriteLine("    story <id> --state In-Progress        Update state");
        Console.WriteLine("    story <id> --notes \"text\"             Append notes");
        Console.WriteLine("    story <id> --actuals 8.5              Update hours");
        Console.WriteLine("    sprint                                Show current sprint info");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  GitLab");
        Console.ResetColor();
        Console.WriteLine("    project [search]                      Discover projects");
        Console.WriteLine("    project --tree <path>                 Show repository tree");
        Console.WriteLine("    branch <story-id> <project>           Create a branch");
        Console.WriteLine("    mr <story-id> <project> <branch>      Create a merge request");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Planning");
        Console.ResetColor();
        Console.WriteLine("    plan <story-id> <project>             Generate execution plan");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Shell");
        Console.ResetColor();
        Console.WriteLine("    help, ?                               Show this help");
        Console.WriteLine("    clear, cls                            Clear screen");
        Console.WriteLine("    exit, quit, q                         Exit RAVEN");
        Console.WriteLine();
    }

    /// <summary>
    /// Parses a shell input line into args, respecting quoted strings.
    /// "story US12345 --notes \"my note here\"" → ["story", "US12345", "--notes", "my note here"]
    /// </summary>
    private static string[] ParseShellArgs(string input)
    {
        var args = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var quoteChar = '"';

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (!inQuotes && (c == '"' || c == '\''))
            {
                inQuotes = true;
                quoteChar = c;
            }
            else if (inQuotes && c == quoteChar)
            {
                inQuotes = false;
            }
            else if (!inQuotes && c == ' ')
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return args.ToArray();
    }

    /// <summary>
    /// Builds the service provider with all RAVEN agents registered.
    /// GitLab auth uses OAuth Bearer tokens (clientId/clientSecret from ~/.gitconfig).
    /// No PAT required.
    /// </summary>
    public static async Task<(ServiceProvider Provider, RavenOrchestrator Orchestrator)> BuildServicesAsync(
        CancellationToken ct = default)
    {
        var config = await ConfigManager.LoadAsync(ct);

        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        services.AddSingleton(config);
        services.AddSingleton(config.Rally);
        services.AddSingleton(config.GitLab);
        services.AddSingleton(config.Preferences);

        // OAuth service for GitLab Bearer token auth (reads clientId/clientSecret from config)
        services.AddSingleton<GitLabOAuthService>();

        services.AddSingleton<RelayAgent>();
        services.AddSingleton<ScoutAgent>();
        services.AddSingleton<ForgeAgent>();
        services.AddSingleton<PlannerAgent>();
        services.AddSingleton<RavenOrchestrator>();

        var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<RavenOrchestrator>();
        orchestrator.RegisterAgent(provider.GetRequiredService<RelayAgent>());
        orchestrator.RegisterAgent(provider.GetRequiredService<ScoutAgent>());
        orchestrator.RegisterAgent(provider.GetRequiredService<ForgeAgent>());
        orchestrator.RegisterAgent(provider.GetRequiredService<PlannerAgent>());

        return (provider, orchestrator);
    }
}
