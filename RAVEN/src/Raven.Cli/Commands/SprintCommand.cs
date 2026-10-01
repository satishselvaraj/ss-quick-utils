using System.CommandLine;
using Raven.Core.Configuration;
using Raven.Relay;

namespace Raven.Cli.Commands;

/// <summary>
/// raven sprint - Shows current sprint information.
/// </summary>
internal static class SprintCommand
{
    public static Command Create()
    {
        var command = new Command("sprint", "Show current sprint information");

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

            ConsoleHelper.WriteHeader("Current Sprint");

            var result = await relay.GetCurrentSprintAsync();
            if (!result.Success || result.Data is null)
            {
                ConsoleHelper.WriteWarning($"No active sprint: {result.Error}");
                return;
            }

            var sprint = result.Data;
            Console.WriteLine($"  Name:     {sprint.Name}");
            Console.WriteLine($"  State:    {sprint.State}");
            Console.WriteLine($"  Start:    {sprint.StartDate:yyyy-MM-dd}");
            Console.WriteLine($"  End:      {sprint.EndDate:yyyy-MM-dd}");
            Console.WriteLine($"  Velocity: {sprint.PlannedVelocity?.ToString("F0") ?? "N/A"} points");

            if (sprint.StartDate.HasValue && sprint.EndDate.HasValue)
            {
                var remaining = (sprint.EndDate.Value - DateTime.UtcNow).Days;
                Console.WriteLine($"  Days Left: {Math.Max(0, remaining)}");
            }

            Console.WriteLine();
        });

        return command;
    }
}
