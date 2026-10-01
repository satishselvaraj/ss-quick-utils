using System.CommandLine;
using Raven.Core.Configuration;
using Raven.Planner;
using Raven.Relay;
using Raven.Scout;

namespace Raven.Cli.Commands;

/// <summary>
/// raven plan <story-id> <project-path> - Generates an execution plan for a work item.
/// </summary>
internal static class PlanCommand
{
    public static Command Create()
    {
        var storyArg = new Argument<string>("story-id", "Rally FormattedID (e.g., US12345)");
        var projectArg = new Argument<string>("project", "GitLab project path or ID");

        var command = new Command("plan", "Generate an execution plan for a Rally work item")
        {
            storyArg,
            projectArg
        };

        command.SetHandler(async (string storyId, string projectPath) =>
        {
            if (!ConfigManager.IsInitialized())
            {
                ConsoleHelper.WriteError("RAVEN not initialized. Run: raven init");
                return;
            }

            var (provider, orchestrator) = await Program.BuildServicesAsync();
            await using var _ = provider;

            var relay = orchestrator.GetAgent<RelayAgent>("relay");
            var scout = orchestrator.GetAgent<ScoutAgent>("scout");
            var planner = orchestrator.GetAgent<PlannerAgent>("planner");

            if (relay is null || scout is null || planner is null)
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
            ConsoleHelper.WriteSuccess($"Found: {workItem.FormattedId} - {workItem.Name}");

            // Step 2: Resolve the project and analyze impact
            ConsoleHelper.WriteAgent("scout", $"Analyzing repository '{projectPath}'...");
            var projectResult = await scout.GetProjectAsync(projectPath);
            if (!projectResult.Success || projectResult.Data is null)
            {
                ConsoleHelper.WriteError($"Project not found: {projectResult.Error}");
                return;
            }

            var project = projectResult.Data;

            // Extract search terms from the work item name
            var searchTerms = workItem.Name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !IsCommonWord(w))
                .ToList();

            ConsoleHelper.WriteAgent("scout", $"Searching for affected files ({searchTerms.Count} terms)...");
            var impactResult = await scout.AnalyzeImpactAsync(project.Id, searchTerms);
            var affectedFiles = impactResult.Success ? impactResult.Data ?? [] : [];

            if (affectedFiles.Count > 0)
            {
                ConsoleHelper.WriteInfo($"Found {affectedFiles.Count} potentially affected files");
            }
            else
            {
                ConsoleHelper.WriteWarning("No files matched - generating generic plan");
            }

            // Step 3: Generate the plan
            ConsoleHelper.WriteAgent("planner", "Generating execution plan...");
            var planResult = await planner.GeneratePlanAsync(workItem, affectedFiles);

            if (!planResult.Success || planResult.Data is null)
            {
                ConsoleHelper.WriteError($"Plan generation failed: {planResult.Error}");
                return;
            }

            var plan = planResult.Data;

            // Display the plan
            ConsoleHelper.WriteHeader($"Execution Plan: {plan.StoryId}");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  Risk Level:     {plan.Risk}");
            Console.WriteLine($"  Estimated Time: {plan.EstimatedHours} hours");
            Console.ResetColor();

            Console.WriteLine();
            ConsoleHelper.WriteInfo("Tasks:");
            for (var i = 0; i < plan.Tasks.Count; i++)
            {
                Console.WriteLine($"    {i + 1}. {plan.Tasks[i]}");
            }

            if (plan.Dependencies.Count > 0)
            {
                Console.WriteLine();
                ConsoleHelper.WriteWarning("Dependencies:");
                foreach (var dep in plan.Dependencies)
                {
                    Console.WriteLine($"    • {dep}");
                }
            }

            if (affectedFiles.Count > 0)
            {
                Console.WriteLine();
                ConsoleHelper.WriteInfo("Affected Files:");
                foreach (var file in affectedFiles.Take(20))
                {
                    Console.WriteLine($"    📄 {file}");
                }
                if (affectedFiles.Count > 20)
                {
                    Console.WriteLine($"    ... and {affectedFiles.Count - 20} more");
                }
            }

            Console.WriteLine();
        }, storyArg, projectArg);

        return command;
    }

    private static bool IsCommonWord(string word)
    {
        var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "with", "from", "that", "this", "have", "will",
            "should", "could", "would", "when", "where", "what", "which", "into",
            "update", "create", "implement", "add", "remove", "fix", "change"
        };
        return common.Contains(word);
    }
}
