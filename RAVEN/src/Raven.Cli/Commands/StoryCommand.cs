using System.CommandLine;
using Raven.Core.Configuration;
using Raven.Relay;

namespace Raven.Cli.Commands;

/// <summary>
/// raven story <id> - Shows details of a specific Rally work item and allows updates.
/// </summary>
internal static class StoryCommand
{
    public static Command Create()
    {
        var idArg = new Argument<string>("id", "Rally FormattedID (e.g., US12345, DE54321)");

        var updateStateOpt = new Option<string?>("--state", "Update schedule state (e.g., In-Progress, Completed)");
        var addNotesOpt = new Option<string?>("--notes", "Append notes to the work item");
        var actualsOpt = new Option<double?>("--actuals", "Update task actuals (hours)");

        var command = new Command("story", "View or update a Rally work item")
        {
            idArg,
            updateStateOpt,
            addNotesOpt,
            actualsOpt
        };

        command.SetHandler(async (string id, string? state, string? notes, double? actuals) =>
        {
            if (!ConfigManager.IsInitialized())
            {
                ConsoleHelper.WriteError("RAVEN not initialized. Run: raven init");
                return;
            }

            var (provider, orchestrator) = await Program.BuildServicesAsync();
            await using var _ = provider;

            var relay = orchestrator.GetAgent<RelayAgent>("relay");
            if (relay is null)
            {
                ConsoleHelper.WriteError("Relay agent not available.");
                return;
            }

            // Fetch the work item
            var result = await relay.GetWorkItemAsync(id);
            if (!result.Success || result.Data is null)
            {
                ConsoleHelper.WriteError($"Work item not found: {result.Error}");
                return;
            }

            var item = result.Data;

            // Display details
            ConsoleHelper.WriteHeader($"{item.FormattedId} - {item.Name}");
            Console.WriteLine($"  Type:      {item.Type}");
            Console.WriteLine($"  State:     {item.ScheduleState}");
            Console.WriteLine($"  Estimate:  {item.PlanEstimate?.ToString("F1") ?? "N/A"} points");
            Console.WriteLine($"  Actuals:   {item.TaskActualTotal?.ToString("F1") ?? "0"} hours");
            Console.WriteLine($"  Remaining: {item.TaskRemainingTotal?.ToString("F1") ?? "N/A"} hours");
            Console.WriteLine($"  Owner:     {item.Owner ?? "Unassigned"}");
            Console.WriteLine($"  Sprint:    {item.Iteration ?? "None"}");
            Console.WriteLine($"  Project:   {item.Project ?? "N/A"}");

            if (item.Tags.Count > 0)
                Console.WriteLine($"  Tags:      {string.Join(", ", item.Tags)}");

            if (!string.IsNullOrEmpty(item.Description))
            {
                Console.WriteLine();
                ConsoleHelper.WriteInfo("Description:");
                // Strip HTML tags for console display
                var plainDesc = System.Text.RegularExpressions.Regex.Replace(item.Description, "<[^>]+>", " ").Trim();
                Console.WriteLine($"  {plainDesc}");
            }

            // Apply updates if requested
            var updated = false;

            if (!string.IsNullOrEmpty(state))
            {
                Console.WriteLine();
                ConsoleHelper.WriteAgent("relay", $"Updating state to '{state}'...");
                var updateResult = await relay.UpdateStateAsync(item, state);
                if (updateResult.Success)
                    ConsoleHelper.WriteSuccess($"State updated to '{state}'");
                else
                    ConsoleHelper.WriteError($"State update failed: {updateResult.Error}");
                updated = true;
            }

            if (!string.IsNullOrEmpty(notes))
            {
                ConsoleHelper.WriteAgent("relay", "Appending notes...");
                var notesResult = await relay.AppendNotesAsync(item, notes);
                if (notesResult.Success)
                    ConsoleHelper.WriteSuccess("Notes updated");
                else
                    ConsoleHelper.WriteError($"Notes update failed: {notesResult.Error}");
                updated = true;
            }

            if (actuals.HasValue)
            {
                ConsoleHelper.WriteAgent("relay", $"Updating actuals to {actuals.Value} hours...");
                var actualsResult = await relay.UpdateActualsAsync(item, actuals.Value);
                if (actualsResult.Success)
                    ConsoleHelper.WriteSuccess($"Actuals updated to {actuals.Value} hours");
                else
                    ConsoleHelper.WriteError($"Actuals update failed: {actualsResult.Error}");
                updated = true;
            }

            if (!updated)
            {
                Console.WriteLine();
                ConsoleHelper.WriteInfo("Use --state, --notes, or --actuals to update this item.");
            }

            Console.WriteLine();
        }, idArg, updateStateOpt, addNotesOpt, actualsOpt);

        return command;
    }
}
