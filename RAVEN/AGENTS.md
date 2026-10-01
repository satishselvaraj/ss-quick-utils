# AGENTS.md - AI Agent Context for RAVEN

## Project Identity

**RAVEN (Rally-Aware AI Virtual Engineering Navigator)** is a multi-agent engineering platform that connects Rally (Broadcom Agile Central), GitLab, and developer tools into a unified software delivery experience. It provides intelligent guidance from requirement to release while maintaining complete traceability.

## Repository Structure

```
RAVEN/
├── RAVEN.sln                          # Solution file
├── Directory.Build.props              # Shared build properties (net10.0)
├── AGENTS.md                          # This file
├── src/
│   ├── Raven.Core/                    # Central orchestration, config, shared models
│   │   ├── Agents/                    # Agent interfaces and result types
│   │   ├── Configuration/             # ConfigManager + RavenConfig (~/.raven/config.json)
│   │   ├── Models/
│   │   │   ├── Rally/                 # RallyWorkItem, RallyIteration
│   │   │   └── GitLab/               # GitLabProject, GitLabBranch, GitLabMergeRequest
│   │   ├── Orchestrator/              # RavenOrchestrator (agent coordinator)
│   │   └── Workflow/                  # WorkflowContext, ExecutionPlan
│   ├── Raven.Relay/                   # Rally API integration agent
│   │   ├── RallyApiClient.cs          # WSAPI v2.0 HTTP client
│   │   └── RelayAgent.cs              # Agent: stories, defects, state, notes, actuals
│   ├── Raven.Scout/                   # GitLab repository intelligence agent
│   │   ├── GitLabApiClient.cs         # GitLab REST API v4 HTTP client
│   │   └── ScoutAgent.cs              # Agent: discovery, mapping, impact analysis
│   ├── Raven.Forge/                   # Git operations agent
│   │   └── ForgeAgent.cs              # Agent: branches, MRs, naming conventions
│   ├── Raven.Planner/                 # Story-to-implementation planner agent
│   │   └── PlannerAgent.cs            # Agent: task generation, risk, estimation
│   └── Raven.Cli/                     # Command-line interface
│       ├── Program.cs                 # Entry point + DI setup
│       ├── ConsoleHelper.cs           # Console formatting utilities
│       └── Commands/                  # CLI commands (init, status, work, story, etc.)
└── Tool/RAVEN/                        # Design documents (reference only)
```

## Technology Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 (`net10.0`) |
| Language | C# (latest) |
| CLI Framework | System.CommandLine |
| DI | Microsoft.Extensions.DependencyInjection |
| Logging | Microsoft.Extensions.Logging |
| HTTP | System.Net.Http.HttpClient |
| JSON | System.Text.Json |
| Storage | File-based JSON in `~/.raven/` |

## Key Design Decisions

1. **No database**: All configuration and credentials stored in `~/.raven/config.json`. Cache files in `~/.raven/cache/`.
2. **Agent architecture**: Each agent (Relay, Scout, Forge, Planner) implements `IAgent` and is registered with `RavenOrchestrator`.
3. **Rally WSAPI v2.0**: Direct HTTP calls to Rally's REST API using API key authentication.
4. **GitLab REST API v4**: Direct HTTP calls using Personal Access Token.
5. **Naming conventions**: Branches follow `feature/US12345-slug` or `bugfix/DE54321-slug` patterns.

## Phase 1 Scope (Current)

- Story Management: Connect Rally, load stories/defects, parse metadata
- Development: Repository discovery, branch creation, MR creation
- Synchronization: Update Rally notes, state, and actuals

## Agent Guidelines

1. **Never log or expose credentials**. Rally API keys and GitLab PATs are stored in `~/.raven/config.json` and should never appear in logs or output.
2. **Each agent is independent**. Agents communicate through the `WorkflowContext` and `AgentResult<T>` types.
3. **The CLI is the primary interface** for Phase 1. Future phases will add web UI and IDE extensions.
4. **Rally API uses ZSESSIONID header** for authentication, not Bearer tokens.
5. **GitLab API uses PRIVATE-TOKEN header** for authentication.
