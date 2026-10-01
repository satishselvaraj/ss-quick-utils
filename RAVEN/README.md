# RAVEN
## Rally-Aware AI Virtual Engineering Navigator

RAVEN is a multi-agent engineering platform that connects **Rally (Broadcom Agile Central)**, **GitLab**, and **GitLab Duo** into a unified software delivery experience. It provides intelligent guidance from requirement to release while maintaining complete traceability.

---

## Prerequisites

- **.NET 10 SDK** installed
- **GitLab OAuth Application** credentials (clientId/clientSecret) in `~/.gitconfig`
- **Rally API Key** in `~/.gitconfig`
- A browser (for one-time OAuth authorization)

---

## ~/.gitconfig Setup

RAVEN reads all credentials from your existing `~/.gitconfig` file. Add these two sections:

```ini
[credential "https://trgl.gitlab-dedicated.com"]
    gitLabDevClientId = db846752d4655d6754e5a5ef908962bf01475a4fa6a04ab4c15a4e2e86469d55
    gitLabDevClientSecret = gloas_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
    gitLabAuthModes = browser
    provider = gitlab

[credential "https://rally1.rallydev.com"]
    rallyApiKey = _your_rally_api_key_here
    provider = rally
```

> **No PAT required.** RAVEN uses OAuth Bearer tokens generated from the clientId/clientSecret. On first use, your browser opens for a one-time authorization. The token is cached in `~/.raven/token.json` and auto-refreshed.

---

## Build

```powershell
cd RAVEN
dotnet build RAVEN.sln
```

## Run (Interactive Shell)

Just run the project with no arguments to open the **RAVEN interactive shell**:

```powershell
dotnet run --project src\Raven.Cli
```

This launches a `raven>` prompt where you type commands directly:

```
  ██████╗  █████╗ ██╗   ██╗███████╗███╗   ██╗
  ██╔══██╗██╔══██╗██║   ██║██╔════╝████╗  ██║
  ██████╔╝███████║██║   ██║█████╗  ██╔██╗ ██║
  ██╔══██╗██╔══██║╚██╗ ██╔╝██╔══╝  ██║╚██╗██║
  ██║  ██║██║  ██║ ╚████╔╝ ███████╗██║ ╚████║
  ╚═╝  ╚═╝╚═╝  ╚═╝  ╚═══╝  ╚══════╝╚═╝  ╚═══╝

  GitLab: ✓ https://trgl.gitlab-dedicated.com  |  Rally: ✓ https://rally1.rallydev.com

  raven> init
  raven> status
  raven> work
  raven> story US12345
  raven> plan US12345 my-group/my-project
  raven> branch US12345 my-group/my-project
  raven> exit
```

Type `help` for all commands, `clear` to clear screen, `exit` to quit.

### Single-Command Mode (for scripts/CI)

You can also pass commands directly as arguments:

```powershell
dotnet run --project src\Raven.Cli -- status
dotnet run --project src\Raven.Cli -- work
dotnet run --project src\Raven.Cli -- story US12345 --state Completed
```

---

## Getting Started

### Step 1: Initialize RAVEN

```powershell
dotnet run --project src\Raven.Cli -- init
```

RAVEN will:
1. **Auto-detect** GitLab URL, Client ID, Client Secret from `~/.gitconfig`
2. **Auto-detect** Rally URL and API Key from `~/.gitconfig`
3. **Prompt** only for non-secret settings (Rally workspace/project paths, default branch)
4. **Save** config to `~/.raven/config.json` (secrets stay in `.gitconfig`)

### Step 2: Verify Connections

```powershell
dotnet run --project src\Raven.Cli -- status
```

On first run, this will open your browser for GitLab OAuth authorization. After you approve, the token is cached and all subsequent commands work without browser interaction.

### Step 3: View Your Work

```powershell
dotnet run --project src\Raven.Cli -- work
```

Output:
```
  My Work - Current Sprint
  ────────────────────────

  ID        Type       Name                    State     Estimate
  ────────  ─────────  ──────────────────────  ────────  ────────
  US12345   UserStory  Customer API endpoint   Defined   5
  DE54321   Defect     Auth token expiry bug   Defined   3
```

---

## Full Developer Workflow

### 1. View Story Details

```powershell
dotnet run --project src\Raven.Cli -- story US12345
```

### 2. Generate an Execution Plan

```powershell
dotnet run --project src\Raven.Cli -- plan US12345 lab/g-app-gitlab-saas-lab/pso-duo-sandbox/ss-quick-utils
```

Output:
```
  Execution Plan: US12345
  ───────────────────────
  Risk Level:     Medium
  Estimated Time: 10.0 hours

  Tasks:
    1. Update service layer: CustomerService.cs
    2. Update controllers: CustomerController.cs
    3. Create unit tests
    4. Verify build and run tests

  Affected Files:
    📄 src/Services/CustomerService.cs
    📄 src/Controllers/CustomerController.cs
```

### 3. Create a Feature Branch

```powershell
dotnet run --project src\Raven.Cli -- branch US12345 lab/g-app-gitlab-saas-lab/pso-duo-sandbox/ss-quick-utils
```

This creates `feature/US12345-customer-api-endpoint` and auto-updates Rally state to "In-Progress".

### 4. Implement with GitLab Duo

Open the branch in Visual Studio or VS Code. Use **GitLab Duo Chat** to implement based on the plan:

> "I'm working on US12345 - Customer API endpoint. The plan says to update CustomerService.cs and CustomerController.cs. Help me implement the changes."

Or use **Duo Code Suggestions** for inline completions while editing.

### 5. Create a Merge Request

```powershell
dotnet run --project src\Raven.Cli -- mr US12345 lab/g-app-gitlab-saas-lab/pso-duo-sandbox/ss-quick-utils feature/US12345-customer-api-endpoint
```

This creates the MR with Rally metadata in the description and auto-appends the MR link to Rally notes.

### 6. Update Rally When Done

```powershell
dotnet run --project src\Raven.Cli -- story US12345 --state Completed --actuals 8.5 --notes "Implementation complete, MR submitted"
```

---

## Command Reference

| Command | Description |
|---|---|
| `raven init` | Interactive setup (reads ~/.gitconfig, creates ~/.raven/config.json) |
| `raven status` | Check agent health and credential sources |
| `raven work` | List your assigned stories/defects in the current sprint |
| `raven story <id>` | View a Rally work item |
| `raven story <id> --state <state>` | Update Rally schedule state |
| `raven story <id> --notes "text"` | Append notes to Rally work item |
| `raven story <id> --actuals <hours>` | Update task actuals |
| `raven sprint` | Show current sprint information |
| `raven project [search]` | Discover GitLab projects |
| `raven project --tree <path>` | Show repository file tree |
| `raven plan <id> <project>` | Generate execution plan for a work item |
| `raven branch <id> <project>` | Create a branch (auto-names, auto-updates Rally) |
| `raven mr <id> <project> <branch>` | Create a merge request (auto-links to Rally) |

---

## Authentication Architecture

```
~/.gitconfig                         ~/.raven/
  ├─ [credential "https://gitlab"]     ├─ config.json    (non-secret settings)
  │   ├─ gitLabDevClientId             └─ token.json     (cached OAuth token)
  │   └─ gitLabDevClientSecret
  ├─ [credential "https://rally"]
  │   └─ rallyApiKey
  └─ (secrets never leave .gitconfig)

         │
         ▼
   ConfigManager.LoadAsync()
   (merges .gitconfig + config.json)
         │
         ▼
   GitLabOAuthService
   ├─ Load cached token from ~/.raven/token.json
   ├─ If expired → auto-refresh via POST /oauth/token
   ├─ If no token → open browser for OAuth authorize
   │   ├─ Start loopback listener on 127.0.0.1:{random port}
   │   ├─ Browser → GitLab /oauth/authorize
   │   ├─ User approves → redirect to loopback with ?code=...
   │   └─ Exchange code → access_token + refresh_token
   └─ All API calls use: Authorization: Bearer {token}
```

**No PAT. No admin access. No server.** Just clientId/clientSecret from `.gitconfig` and a one-time browser authorization.

---

## Agent Architecture

| Agent | Purpose |
|---|---|
| **Relay** | Rally WSAPI v2.0 (stories, defects, sprints, state/notes/actuals) |
| **Scout** | GitLab REST API v4 (projects, repo tree, impact analysis) |
| **Forge** | Git operations (branches, MRs, naming conventions) |
| **Planner** | Story-to-task planning (task generation, risk, estimation) |

---

## Project Structure

```
RAVEN/
├── RAVEN.sln
├── Directory.Build.props          # net10.0, C# latest
├── README.md                      # This file
├── AGENTS.md                      # AI agent context
├── src/
│   ├── Raven.Core/                # Config, models, orchestrator, OAuth
│   │   ├── Auth/                  # GitLabOAuthService (Bearer token flow)
│   │   ├── Configuration/         # ConfigManager, GitConfigService, RavenConfig
│   │   ├── Models/                # Rally + GitLab data models
│   │   └── Orchestrator/          # Agent coordinator
│   ├── Raven.Relay/               # Rally API integration
│   ├── Raven.Scout/               # GitLab repository intelligence
│   ├── Raven.Forge/               # Git operations (branches, MRs)
│   ├── Raven.Planner/             # Story-to-implementation planning
│   └── Raven.Cli/                 # CLI entry point + commands
└── Tool/RAVEN/                    # Design documents (reference)
```
