# Rally + GitLab Duo Development Agent for Visual Studio 2022/2026 and VS Code

## Overview

Build a lightweight extension and orchestration layer that integrates:

- Rally (Broadcom Agile Central)
- GitLab Repositories
- GitLab Duo Agent
- Visual Studio 2022/2026
- Visual Studio Code

The goal is to allow developers to:

1. Retrieve assigned Rally User Stories and Defects.
2. Parse structured information from Rally Description and Notes fields.
3. Identify the target application/repository.
4. Communicate requirements to GitLab Duo Agent.
5. Generate and review code changes.
6. Create branches, commits, and Merge Requests.
7. Update Rally with implementation details, MR links, effort, and status changes.

The solution should act primarily as an orchestration bridge between Rally, GitLab Duo, and the IDE, with minimal business logic in the extensions themselves.

---

# Objectives

Create a developer assistant capable of:

- Reading assigned Rally work items.
- Understanding implementation requirements from Rally metadata.
- Locating the correct code repository.
- Working with GitLab Duo Agent to implement features and fixes.
- Creating Merge Requests tied to Rally IDs.
- Updating Rally automatically with implementation details.
- Tracking actual effort and updating corresponding Rally fields.
- Running from either Visual Studio or VS Code.

---

# Architecture

```text
+--------------------------------------------------+
| Visual Studio / VS Code Extension                |
+--------------------------------------------------+
                 |
                 |
                 v
+--------------------------------------------------+
| Local Orchestration Service                      |
|                                                  |
|  Rally Connector                                 |
|  GitLab Connector                                |
|  Duo Agent Connector                             |
|  Workflow Engine                                 |
|  State Manager                                   |
+--------------------------------------------------+
        |                   |
        |                   |
        v                   v

+---------------+     +----------------+
| Rally API     |     | GitLab API     |
+---------------+     +----------------+
                              |
                              v
                    +------------------+
                    | GitLab Duo Agent |
                    +------------------+
```

---

# Recommended Technology Stack

## Backend

- .NET 8 Web API
- C#
- REST APIs
- Background Workers

## Visual Studio Extension

- VSIX
- AsyncPackage
- ToolWindowPane
- Commands

## VS Code Extension

- TypeScript
- VS Code Extension API

## External Systems

- Rally Web Services API
- GitLab API
- GitLab Duo Agent

---

# Authentication

Provide a secure configuration mechanism.

```json
{
  "rallyApiKey": "YOUR_RALLY_API_KEY",
  "gitlabToken": "YOUR_GITLAB_TOKEN",
  "gitlabUrl": "https://gitlab.company.com",
  "gitlabProject": "ProjectName"
}
```

## Secure Storage

### Visual Studio

- Windows Credential Manager
- Visual Studio Settings Store

### VS Code

- vscode.SecretStorage

---

# Rally Integration

## Retrieve Assigned User Stories

Query Rally for current user's assigned work items.

Filters:

```text
Owner = Current User
ScheduleState != Closed
```

Retrieve:

- FormattedID
- Name
- Description
- Notes
- ScheduleState
- Owner
- PlanEstimate
- Actuals

Example:

```json
{
  "FormattedID": "US12345",
  "Name": "Add Customer API",
  "Description": "...",
  "Notes": "..."
}
```

---

# Story Metadata Format

Stories and defects may contain structured metadata.

Example:

```text
App Name: Customer Portal

Details:
Add support for the customer onboarding API.

Issue:
Authentication errors occur when token refresh expires.
```

---

# Metadata Parser

The orchestration service must extract:

## App Name

```text
Customer Portal
```

## Details

```text
Add support for the customer onboarding API.
```

## Issue

```text
Authentication errors occur when token refresh expires.
```

Output:

```json
{
  "appName": "Customer Portal",
  "details": "Add support for the customer onboarding API.",
  "issue": "Authentication errors occur when token refresh expires."
}
```

---

# Repository Resolution

Map Rally applications to GitLab repositories.

Example:

```json
{
  "Customer Portal": "customer-portal",
  "Billing API": "billing-api",
  "Order Service": "order-service"
}
```

Workflow:

1. Determine application.
2. Locate repository.
3. Pull latest changes.
4. Open workspace.
5. Create working branch.

Example branch:

```text
feature/US12345-add-customer-api
```

Defect branch:

```text
bugfix/DE54321-auth-fix
```

---

# GitLab Duo Agent Integration

Construct contextual prompts for GitLab Duo Agent.

Example:

```text
Rally ID: US12345

Application:
Customer Portal

Story Details:
Add support for the customer onboarding API.

Issue:
Authentication errors occur when token refresh expires.

Tasks:
1. Analyze the repository.
2. Identify affected files.
3. Implement required changes.
4. Generate unit tests.
5. Describe all modifications.
6. Follow project conventions.
```

---

# Expected Duo Agent Output

The system should expect:

- Modified files
- New files
- Unit tests
- Implementation summary
- Technical explanation

Example:

```text
Modified Files:
- CustomerController.cs
- CustomerService.cs

Added Files:
- CustomerTests.cs

Summary:
Implemented onboarding API endpoint.
Added validation.
Created unit tests.
```

---

# Human Approval Gate

No automatic commits should occur without approval.

Workflow:

```text
1. Present proposed changes.
2. Review diff.
3. Approve or reject.
4. Commit only after approval.
```

Commands:

```text
Approve
Reject
Regenerate
```

---

# Commit Format

User Stories:

```text
US12345 - Implement Customer Onboarding API
```

Defects:

```text
DE54321 - Fix Token Refresh Authentication Failure
```

---

# Merge Request Creation

Create GitLab Merge Requests automatically.

Title:

```text
US12345 - Implement Customer Onboarding API
```

Description:

```markdown
## Rally Item

US12345

## Summary

Implemented onboarding API endpoint.

## Changes

- Added new endpoint
- Added validation
- Added service layer updates
- Added unit tests
```

---

# Rally Update

After successful Merge Request creation, update Rally.

## Notes Update

```text
Implementation Complete

Merge Request:
https://gitlab.company.com/project/-/merge_requests/114

Changes:
- Added onboarding API
- Added validation
- Added tests
```

---

# Rally State Updates

Proposed state mapping:

| Event | Rally State |
|---------|---------|
| Story Retrieved | Defined |
| Development Started | In-Progress |
| MR Created | Completed |
| MR Approved | Accepted |
| Production Deployment | Released |

---

# Effort Tracking

Track:

```text
Start Time
End Time
Actual Duration
```

Update Rally fields:

```json
{
  "Actuals": 4.5
}
```

Example:

```text
Estimated: 3
Actual: 4.5
```

---

# Extension Commands

## Rally Commands

```text
Rally: Load My Work
Rally: Refresh Work Items
Rally: Open Story
Rally: Open Defect
```

## Development Commands

```text
Rally: Implement Story Using Duo
Rally: Fix Defect Using Duo
```

## Git Commands

```text
GitLab: Create Branch
GitLab: Commit Changes
GitLab: Create Merge Request
```

## Synchronization Commands

```text
Rally: Update Status
Rally: Post Implementation Summary
Rally: Sync Current Work
```

---

# Visual Studio Extension Structure

```text
RallyDuoExtension

├── Commands
├── ToolWindows
├── Services
├── Rally
├── GitLab
├── Duo
├── Models
├── Configuration
└── Utilities
```

---

# VS Code Extension Structure

```text
rally-duo-extension

├── src
│   ├── commands
│   ├── rally
│   │   └── RallyClient.ts
│   ├── gitlab
│   │   └── GitLabClient.ts
│   ├── duo
│   │   └── DuoAgentClient.ts
│   ├── parsers
│   │   └── RallyParser.ts
│   ├── services
│   │   ├── StoryProcessor.ts
│   │   ├── DefectProcessor.ts
│   │   └── MergeRequestService.ts
│   ├── state
│   └── extension.ts
│
└── package.json
```

---

# Shared Backend Recommendation

To avoid duplicating logic across Visual Studio and VS Code, implement all integrations in a single local service.

Architecture:

```text
Visual Studio Extension
          |
          |
          v

+-------------------------+
| RallyDuo Backend        |
| .NET 8 Web API          |
+-------------------------+

          ^
          |
          |

VS Code Extension
```

Example API:

```http
GET /api/workitems
POST /api/implement
POST /api/create-mr
POST /api/update-rally
```

Benefits:

- Single codebase
- Easier maintenance
- Consistent workflow
- Shared authentication
- Shared business logic

---

# Minimum Viable Product (MVP)

## Phase 1

- Authenticate to Rally
- Load assigned stories
- Load assigned defects
- Parse story metadata
- Resolve repository
- Create working branch
- Communicate with GitLab Duo
- Generate implementation proposal
- Create Merge Request
- Update Rally notes

## Phase 2

- Effort tracking
- Actuals update
- Rally state synchronization
- Automated testing
- CI/CD integration

## Phase 3

- Sprint dashboard
- Multi-story implementation
- Dependency analysis
- AI-generated code reviews
- Team metrics
- Automated release notes

---

# Success Criteria

The solution is considered successful when it can:

1. Retrieve Rally stories and defects assigned to the current developer.
2. Parse application, implementation, and issue details.
3. Open the correct GitLab repository automatically.
4. Coordinate implementation work using GitLab Duo Agent.
5. Generate and review code changes.
6. Create traceable commits and Merge Requests.
7. Update Rally with implementation notes and MR links.
8. Synchronize actual effort and lifecycle status.
9. Operate seamlessly from both Visual Studio and VS Code using a shared orchestration backend.