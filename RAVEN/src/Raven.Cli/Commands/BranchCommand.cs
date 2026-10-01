using System.CommandLine;
using Raven.Core.Configuration;
using Raven.Forge;
using Raven.Relay;

namespace Raven.Cli.Commands;

/// <summary>
/// raven branch <story-id> <project-path> - Creates a branch following RAVEN naming conventions.
/// </summary>
internal static class BranchCommand
{
    public static Command Create()
    {
        var storyArg = new Argument<string>("story-id", "Rally FormattedID (e.g., US12345)");
        var projectArg = new Argument<string>("project", "GitLab project path or ID");
        var baseOpt = new Option<string?>("--base", "Base branch (defaults to configured default branch)");

        var command = new Command("branch", "Create a branch for a Rally work item")
        {
            storyArg,
            projectArg,
            baseOpt
        };

        command.SetHandler(async (string storyId, string projectPath, string? baseBranch) =>
        {
            if (!ConfigManager.IsInitialized())
            {
                ConsoleHelper.WriteError("RAVEN not initialized. Run: raven init");
                return;
            }

            var (provider, orchestrator) = await Program.BuildServicesAsync();
            await using var _ = provider;

            var relay = orchestrator.GetAgent<RelayAgent>("relay");
            var forge = orchestrator.GetAgent<ForgeAgent>("forge");
            var scout = orchestrator.GetAgent<Scout.ScoutAgent>("scout");

            if (relay is null || forge is null || scout is null)
            {
                ConsoleHelper.WriteError("Required agents not available.");
                return;
            }

            // Step 1: Get the work item from Rally
            ConsoleHelper.WriteAgent("relay", $"Fetching {storyId} from Rally...");
            var workItemResult = await relay.GetWorkItemAsync(storyId);
            if (!workItemResult.Success || workItemResult.Data is null)
            {
                ConsoleHelper.WriteError($"Work item not found: {workItemResult.Error}");
                return;
            }

            var workItem = workItemResult.Data;
            ConsoleHelper.WriteSuccess($"Found: {workItem.FormattedId} - {workItem.Name}");

            // Step 2: Resolve the GitLab project
            ConsoleHelper.WriteAgent("scout", $"Resolving project '{projectPath}'...");
            var projectResult = await scout.GetProjectAsync(projectPath);
            if (!projectResult.Success || projectResult.Data is null)
            {
                ConsoleHelper.WriteError($"Project not found: {projectResult.Error}");
                return;
            }

            var project = projectResult.Data;

            // Step 3: Create the branch
            var branchName = forge.GenerateBranchName(workItem);
            ConsoleHelper.WriteAgent("forge", $"Creating branch '{branchName}'...");

            var branchResult = await forge.CreateBranchAsync(project.Id, workItem, baseBranch);
            if (!branchResult.Success || branchResult.Data is null)
            {
                ConsoleHelper.WriteError($"Branch creation failed: {branchResult.Error}");
                return;
            }

            ConsoleHelper.WriteSuccess($"Branch created: {branchName}");

            // Step 4: Update Rally state if configured
            var config = await ConfigManager.LoadAsync();
            if (config.Preferences.AutoUpdateRally && workItem.ScheduleState == "Defined")
            {
                ConsoleHelper.WriteAgent("relay", "Updating Rally state to 'In-Progress'...");
                await relay.UpdateStateAsync(workItem, "In-Progress");
                ConsoleHelper.WriteSuccess("Rally state updated to 'In-Progress'");
            }

            Console.WriteLine();
        }, storyArg, projectArg, baseOpt);

        return command;
    }
}
