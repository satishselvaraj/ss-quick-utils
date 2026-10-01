using System.CommandLine;
using Raven.Core.Configuration;
using Raven.Forge;
using Raven.Relay;

namespace Raven.Cli.Commands;

/// <summary>
/// raven mr <story-id> <project-path> <source-branch> - Creates a merge request for a work item.
/// </summary>
internal static class MrCommand
{
    public static Command Create()
    {
        var storyArg = new Argument<string>("story-id", "Rally FormattedID (e.g., US12345)");
        var projectArg = new Argument<string>("project", "GitLab project path or ID");
        var branchArg = new Argument<string>("source-branch", "Source branch name");
        var targetOpt = new Option<string?>("--target", "Target branch (defaults to configured default branch)");

        var command = new Command("mr", "Create a merge request for a Rally work item")
        {
            storyArg,
            projectArg,
            branchArg,
            targetOpt
        };

        command.SetHandler(async (string storyId, string projectPath, string sourceBranch, string? targetBranch) =>
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

            // Step 1: Get the work item
            ConsoleHelper.WriteAgent("relay", $"Fetching {storyId} from Rally...");
            var workItemResult = await relay.GetWorkItemAsync(storyId);
            if (!workItemResult.Success || workItemResult.Data is null)
            {
                ConsoleHelper.WriteError($"Work item not found: {workItemResult.Error}");
                return;
            }

            var workItem = workItemResult.Data;

            // Step 2: Resolve the project
            ConsoleHelper.WriteAgent("scout", $"Resolving project '{projectPath}'...");
            var projectResult = await scout.GetProjectAsync(projectPath);
            if (!projectResult.Success || projectResult.Data is null)
            {
                ConsoleHelper.WriteError($"Project not found: {projectResult.Error}");
                return;
            }

            var project = projectResult.Data;

            // Step 3: Create the MR
            ConsoleHelper.WriteAgent("forge", "Creating merge request...");
            var mrResult = await forge.CreateMergeRequestAsync(
                project.Id, workItem, sourceBranch, targetBranch);

            if (!mrResult.Success || mrResult.Data is null)
            {
                ConsoleHelper.WriteError($"MR creation failed: {mrResult.Error}");
                return;
            }

            var mr = mrResult.Data;
            ConsoleHelper.WriteSuccess($"Merge request created: !{mr.Iid}");
            ConsoleHelper.WriteInfo($"URL: {mr.WebUrl}");

            // Step 4: Update Rally
            var config = await ConfigManager.LoadAsync();
            if (config.Preferences.AutoUpdateRally)
            {
                ConsoleHelper.WriteAgent("relay", "Updating Rally notes with MR link...");
                await relay.AppendNotesAsync(workItem,
                    $"MR created: <a href=\"{mr.WebUrl}\">!{mr.Iid} - {mr.Title}</a>");
                ConsoleHelper.WriteSuccess("Rally notes updated with MR link");
            }

            Console.WriteLine();
        }, storyArg, projectArg, branchArg, targetOpt);

        return command;
    }
}
