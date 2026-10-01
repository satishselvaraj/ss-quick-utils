using System.CommandLine;
using Raven.Core.Configuration;
using Raven.Relay;

namespace Raven.Cli.Commands;

/// <summary>
/// raven work - Lists work assigned to the current user in the active sprint.
/// </summary>
internal static class WorkCommand
{
    public static Command Create()
    {
        var command = new Command("work", "List your assigned stories and defects in the current sprint");

        command.SetHandler(async () =>
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

            ConsoleHelper.WriteHeader("My Work - Current Sprint");

            // Get current sprint info
            var sprintResult = await relay.GetCurrentSprintAsync();
            if (sprintResult.Success && sprintResult.Data is not null)
            {
                var sprint = sprintResult.Data;
                ConsoleHelper.WriteInfo($"Sprint: {sprint.Name} ({sprint.StartDate:MMM dd} - {sprint.EndDate:MMM dd})");
            }

            // Get work items
            var result = await relay.GetMyWorkAsync();

            if (!result.Success)
            {
                ConsoleHelper.WriteError($"Failed to load work: {result.Error}");
                return;
            }

            var items = result.Data!;

            if (items.Count == 0)
            {
                ConsoleHelper.WriteInfo("No work items assigned in the current sprint.");
                return;
            }

            var headers = new[] { "ID", "Type", "Name", "State", "Estimate" };
            var rows = items.Select(w => new[]
            {
                w.FormattedId,
                w.Type.ToString(),
                w.Name,
                w.ScheduleState,
                w.PlanEstimate?.ToString("F0") ?? "-"
            }).ToList();

            ConsoleHelper.WriteTable(headers, rows);
        });

        return command;
    }
}
