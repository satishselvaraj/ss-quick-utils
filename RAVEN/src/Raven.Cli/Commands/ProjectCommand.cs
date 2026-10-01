using System.CommandLine;
using Raven.Core.Configuration;
using Raven.Scout;

namespace Raven.Cli.Commands;

/// <summary>
/// raven project - Discover and inspect GitLab projects.
/// </summary>
internal static class ProjectCommand
{
    public static Command Create()
    {
        var searchArg = new Argument<string?>("search", () => null, "Search term to filter projects");
        var treeOpt = new Option<string?>("--tree", "Show repository tree for a project (path_with_namespace or ID)");

        var command = new Command("project", "Discover and inspect GitLab projects")
        {
            searchArg,
            treeOpt
        };

        command.SetHandler(async (string? search, string? treePath) =>
        {
            if (!ConfigManager.IsInitialized())
            {
                ConsoleHelper.WriteError("RAVEN not initialized. Run: raven init");
                return;
            }

            var (provider, orchestrator) = await Program.BuildServicesAsync();
            await using var _ = provider;

            var scout = orchestrator.GetAgent<ScoutAgent>("scout");
            if (scout is null)
            {
                ConsoleHelper.WriteError("Scout agent not available.");
                return;
            }

            // If --tree is specified, show repository structure
            if (!string.IsNullOrEmpty(treePath))
            {
                var projectResult = await scout.GetProjectAsync(treePath);
                if (!projectResult.Success || projectResult.Data is null)
                {
                    ConsoleHelper.WriteError($"Project not found: {projectResult.Error}");
                    return;
                }

                var project = projectResult.Data;
                ConsoleHelper.WriteHeader($"Repository: {project.FullName}");
                ConsoleHelper.WriteInfo($"URL: {project.WebUrl}");
                ConsoleHelper.WriteInfo($"Default branch: {project.DefaultBranch}");
                Console.WriteLine();

                var treeResult = await scout.MapRepositoryAsync(project.Id);
                if (!treeResult.Success || treeResult.Data is null)
                {
                    ConsoleHelper.WriteError($"Failed to map repository: {treeResult.Error}");
                    return;
                }

                foreach (var item in treeResult.Data.OrderBy(i => i.Type == "tree" ? 0 : 1).ThenBy(i => i.Name))
                {
                    var icon = item.IsDirectory ? "📁" : "📄";
                    Console.WriteLine($"  {icon} {item.Path}");
                }

                Console.WriteLine();
                return;
            }

            // Otherwise, list/search projects
            ConsoleHelper.WriteHeader("GitLab Projects");

            var result = await scout.DiscoverProjectsAsync(search);
            if (!result.Success || result.Data is null)
            {
                ConsoleHelper.WriteError($"Discovery failed: {result.Error}");
                return;
            }

            var headers = new[] { "ID", "Name", "Path", "Branch" };
            var rows = result.Data.Select(p => new[]
            {
                p.Id.ToString(),
                p.Name,
                p.PathWithNamespace,
                p.DefaultBranch
            }).ToList();

            ConsoleHelper.WriteTable(headers, rows);
        }, searchArg, treeOpt);

        return command;
    }
}
