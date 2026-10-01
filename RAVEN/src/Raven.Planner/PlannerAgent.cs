using Microsoft.Extensions.Logging;
using Raven.Core.Agents;
using Raven.Core.Models.Rally;
using Raven.Core.Workflow;

namespace Raven.Planner;

/// <summary>
/// RAVEN Planner Agent - Story-to-implementation planner.
/// Analyzes requirements, generates technical tasks, identifies dependencies, and estimates effort.
/// </summary>
public sealed class PlannerAgent : IAgent
{
    public string Name => "planner";
    public string Description => "Story-to-implementation planner - analyzes requirements and generates execution plans";

    private readonly ILogger<PlannerAgent> _logger;

    public PlannerAgent(ILogger<PlannerAgent> logger)
    {
        _logger = logger;
    }

    public Task<AgentHealthCheck> CheckHealthAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new AgentHealthCheck(Name, true, "Planner agent ready"));
    }

    /// <summary>
    /// Generates an execution plan from a Rally work item and affected files.
    /// Analyzes the story/defect description and repository context to produce tasks.
    /// </summary>
    public Task<AgentResult<ExecutionPlan>> GeneratePlanAsync(
        RallyWorkItem workItem, IReadOnlyList<string> affectedFiles, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Planner: Generating plan for {Id} - {Name}",
                workItem.FormattedId, workItem.Name);

            var plan = new ExecutionPlan
            {
                StoryId = workItem.FormattedId,
                Tasks = GenerateTasks(workItem, affectedFiles),
                Risk = AssessRisk(workItem, affectedFiles),
                Dependencies = IdentifyDependencies(affectedFiles),
                EstimatedHours = EstimateEffort(workItem, affectedFiles)
            };

            _logger.LogInformation("Planner: Generated {Count} tasks, risk={Risk}, est={Hours}h",
                plan.Tasks.Count, plan.Risk, plan.EstimatedHours);

            return Task.FromResult(AgentResult<ExecutionPlan>.Ok(plan, Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Planner: Plan generation failed");
            return Task.FromResult(AgentResult<ExecutionPlan>.Fail(ex.Message, Name));
        }
    }

    private static List<string> GenerateTasks(RallyWorkItem workItem, IReadOnlyList<string> affectedFiles)
    {
        var tasks = new List<string>();

        // Group affected files by layer/pattern
        var controllers = affectedFiles.Where(f => f.Contains("Controller", StringComparison.OrdinalIgnoreCase)).ToList();
        var services = affectedFiles.Where(f => f.Contains("Service", StringComparison.OrdinalIgnoreCase)).ToList();
        var models = affectedFiles.Where(f => f.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
                                               f.Contains("Entity", StringComparison.OrdinalIgnoreCase)).ToList();
        var tests = affectedFiles.Where(f => f.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                                              f.Contains("Spec", StringComparison.OrdinalIgnoreCase)).ToList();
        var configs = affectedFiles.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                                f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                                f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)).ToList();

        // Generate tasks based on affected layers
        if (models.Count > 0)
            tasks.Add($"Update data models: {string.Join(", ", models.Select(Path.GetFileName))}");

        if (services.Count > 0)
            tasks.Add($"Update service layer: {string.Join(", ", services.Select(Path.GetFileName))}");

        if (controllers.Count > 0)
            tasks.Add($"Update controllers: {string.Join(", ", controllers.Select(Path.GetFileName))}");

        if (configs.Count > 0)
            tasks.Add($"Update configuration: {string.Join(", ", configs.Select(Path.GetFileName))}");

        // Always include testing and documentation tasks
        if (tests.Count > 0)
            tasks.Add($"Update existing tests: {string.Join(", ", tests.Select(Path.GetFileName))}");
        else
            tasks.Add("Create unit tests for changes");

        tasks.Add("Verify build and run existing tests");

        // If it's a defect, add verification task
        if (workItem.Type == RallyWorkItemType.Defect)
        {
            tasks.Insert(0, "Reproduce and verify the defect");
            tasks.Add("Verify defect is resolved");
        }

        // If no specific files were identified, generate generic tasks
        if (affectedFiles.Count == 0)
        {
            tasks.Clear();
            tasks.Add("Analyze requirements and identify affected components");
            tasks.Add("Implement changes");
            tasks.Add("Create unit tests");
            tasks.Add("Verify build and run tests");
        }

        return tasks;
    }

    private static string AssessRisk(RallyWorkItem workItem, IReadOnlyList<string> affectedFiles)
    {
        var score = 0;

        // More files = higher risk
        if (affectedFiles.Count > 10) score += 3;
        else if (affectedFiles.Count > 5) score += 2;
        else if (affectedFiles.Count > 0) score += 1;

        // Higher estimate = higher risk
        if (workItem.PlanEstimate > 8) score += 3;
        else if (workItem.PlanEstimate > 5) score += 2;
        else if (workItem.PlanEstimate > 3) score += 1;

        // Config changes add risk
        if (affectedFiles.Any(f => f.EndsWith(".json") || f.EndsWith(".yml") || f.EndsWith(".yaml")))
            score += 1;

        // Defects are inherently riskier (unknown scope)
        if (workItem.Type == RallyWorkItemType.Defect)
            score += 1;

        return score switch
        {
            <= 2 => "Low",
            <= 4 => "Medium",
            _ => "High"
        };
    }

    private static List<string> IdentifyDependencies(IReadOnlyList<string> affectedFiles)
    {
        var deps = new List<string>();

        if (affectedFiles.Any(f => f.Contains("Controller")))
            deps.Add("API layer changes may require client updates");

        if (affectedFiles.Any(f => f.Contains("Migration") || f.Contains("Schema")))
            deps.Add("Database migration required");

        if (affectedFiles.Any(f => f.EndsWith(".csproj") || f.EndsWith("package.json")))
            deps.Add("Package dependency changes");

        if (affectedFiles.Any(f => f.Contains("ci") || f.Contains("pipeline") || f.Contains("yml")))
            deps.Add("CI/CD pipeline changes");

        return deps;
    }

    private static double EstimateEffort(RallyWorkItem workItem, IReadOnlyList<string> affectedFiles)
    {
        // Use Rally estimate as base, adjust by file count
        var baseHours = (workItem.PlanEstimate ?? 3) * 2; // rough: 1 point ≈ 2 hours

        // Adjust for file count
        var fileMultiplier = affectedFiles.Count switch
        {
            0 => 1.0,
            <= 3 => 1.0,
            <= 7 => 1.2,
            <= 15 => 1.5,
            _ => 2.0
        };

        return Math.Round(baseHours * fileMultiplier, 1);
    }
}
