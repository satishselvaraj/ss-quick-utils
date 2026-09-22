# AGENTS.md - Project Context for AI Agents

## Project Identity

**SS-Quick-Utils** is a collection of utilities for automating GitLab OAuth token generation for the Visual Studio GitLab extension (Duo Chat, Code Suggestions, Code Review). It contains four versions of the OAuth Token Generator, each solving the same problem with increasing levels of integration.

## Repository Structure

```
SS-quick-utils/
├── OAuthTokenGen/          # V1 - .NET Console Application (interactive CLI)
├── OAuthTokenGenV2/        # V2 - Standalone Windows Executable (self-contained .exe)
├── OAuthTokenGenV3/        # V3 - Web App (Docker, .NET Kestrel, GitLab Pages)
├── OAuthTokenGenExt/       # V4 - Visual Studio 2022/2026 Extension (VSIX + WebView2)
├── .gitlab-ci.yml          # CI/CD pipeline (GitLab Pages deployment for V3)
├── AGENTS.md               # This file
├── CONTRIBUTING.md          # Contributor guidelines
├── README.md               # Root documentation (all versions)
└── ReviewCode.md            # Code review reference
```

## Version Evolution

| Version | Folder | Type | Key Innovation |
|---|---|---|---|
| **V1** | `OAuthTokenGen/` | .NET Console App | First automation of the OAuth flow |
| **V2** | `OAuthTokenGenV2/` | Standalone EXE | No .NET SDK required, token refresh, JSON storage |
| **V3** | `OAuthTokenGenV3/` | Static Web App | Browser-based UI, Docker, GitLab Pages, export/import |
| **V4** | `OAuthTokenGenExt/` | VS Extension | Embedded in IDE, no server, no admin, VSIX distribution |

## V4 Extension (OAuthTokenGenExt) - Technical Context

The newest addition. A Visual Studio 2022/2026 extension built with:

- **VSSDK (in-process)** extensibility model targeting `net472`
- **WebView2** control hosting an HTML/CSS/JS UI adapted from V3
- **C#/JS bridge** via `postMessage` / `WebMessageReceived` for persistence
- **%APPDATA% JSON files** for settings and token storage
- **No admin access** required (per-user VSIX install)

The extension was built because V3 requires running a local server (Docker, .NET, or Python), which is not always possible in locked-down enterprise environments. V4 eliminates all external dependencies by running entirely inside Visual Studio.

#### Build Requirements
- .NET SDK (any version that supports `net472` targeting)
- Visual Studio 2022 or 2026 (for VSIX packaging; `dotnet build` compiles the DLL but not the VSIX container)

#### Key Files
- `OAuthTokenGenExt/DESIGN.md` - Detailed architecture and build decisions
- `OAuthTokenGenExt/README.md` - Usage, installation, and feature documentation
- `OAuthTokenGenExt/TokenGeneratorControl.xaml.cs` - Core C#/JS bridge logic
- `OAuthTokenGenExt/Resources/app.js` - OAuth flow and token management logic

## Conventions

- **Language**: C# for backend/CLI, HTML/CSS/JS for web UI, PowerShell for scripts
- **Target framework**: `net8.0` for V1-V3, `net472` for V4 (VSSDK requirement)
- **OAuth flow**: Authorization Code grant (`response_type=code`) with CSRF `state` parameter
- **Storage**: JSON files (V2 local, V4 %APPDATA%), browser localStorage (V3)
- **GitLab instance**: Self-managed GitLab Dedicated at `https://trgl.gitlab-dedicated.com`
- **Runner tags**: `dex-build-poc` required for CI/CD jobs on this instance

## Agent Guidelines

When working on this project:

1. **Preserve version independence**: Each version (V1-V4) is self-contained. Changes to one should not break others.
2. **V4 targets Visual Studio IDE** (not VS Code). The extension uses VSSDK, WPF, WebView2, and `ToolWindowPane`.
3. **OAuth secrets are user-scoped**: V3 uses browser localStorage, V4 uses `%APPDATA%`. Never log or expose tokens.
4. **The V3 web UI is the source of truth** for feature parity. V4's HTML/CSS/JS was adapted from V3's `wwwroot/` files.
5. **VSIX packaging requires Visual Studio**: `dotnet build` compiles the DLL but the `.vsix` container needs MSBuild from within VS.
6. **No admin access assumption**: V4 was specifically designed for environments where users cannot install Docker, run local servers, or elevate privileges.
