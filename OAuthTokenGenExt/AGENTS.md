# AGENTS.md - AI Agent Context for OAuthTokenGenExt

## Project Identity

**OAuthTokenGenExt** is a Visual Studio 2022/2026 extension that generates OAuth 2.0 access tokens for the **GitLab for Visual Studio** extension (Duo Chat, Code Suggestions, Code Review). It uses a native WPF tool window, reads credentials from `~/.gitconfig`, auto-refreshes tokens every 2 hours, and pushes them directly into the GitLab Duo extension settings via the DTE automation API.

This project originated as the fourth iteration (V4) of the GitLab OAuth Token Generator, evolving from a console app (V1) to a standalone EXE (V2) to a web app (V3) to this IDE-embedded extension. The V3 web app's WebView2 approach was replaced with native WPF in the current version.

## Repository Structure

```
OAuthTokenGenExt/
├── OAuthTokenGenExt.csproj              # Project: net472, VSSDK, System.Text.Json
├── OAuthTokenGenExt.sln                 # Solution file for VS IDE
├── source.extension.vsixmanifest        # VSIX manifest: VS 2022 [17.0, 19.0) targets
├── OAuthTokenGenExtPackage.cs           # AsyncPackage: registers command + tool window
├── OAuthTokenGenExtPackage.vsct         # Command table: Tools menu item GUIDs
├── OpenTokenGeneratorCommand.cs         # MenuCommand: opens TokenGeneratorToolWindow
├── TokenGeneratorToolWindow.cs          # ToolWindowPane: caption + WPF content host
├── TokenGeneratorControl.xaml           # WPF XAML: 4 sections (Status, Credentials read-only, Authorize, Log)
├── TokenGeneratorControl.xaml.cs        # Core logic: .gitconfig load, one-click OAuth, auto-refresh, DirectSetAndSave
├── Services/
│   ├── GitConfigService.cs              # Read/write gitLabDevClientId/Secret from ~/.gitconfig
│   ├── OAuthTokenService.cs             # HttpClient: token exchange, refresh, validation
│   ├── LoopbackHttpListener.cs          # Temporary HttpListener on 127.0.0.1 for OAuth callback
│   └── GitLabDuoSettingsService.cs      # DirectSetAndSave: sets on GeneralSettings.Instance + Save()
├── Properties/
│   └── launchSettings.json              # F5 debug profiles (VS 2022/2026 Experimental Instance)
├── build.ps1                            # Build script: dotnet build + VSIXInstaller
├── AGENTS.md                            # This file
├── DESIGN.md                            # Architecture, build steps, design decisions
├── README.md                            # Full documentation, usage, troubleshooting
└── Resources/
    └── icon.png                         # 32x32 extension icon
```

## Technology Stack

| Layer | Technology | Why |
|---|---|---|
| **Extensibility** | VSSDK (in-process), `AsyncPackage` | WritableSettingsStore + reflection for Duo settings, VS 2022 + 2026 compat |
| **Target framework** | .NET Framework 4.7.2 (`net472`) | Required by VSSDK in-process model |
| **UI** | Native WPF (`UserControl`, `StackPanel`, `TextBox`, `Button`) | No WebView2 dependency, VS theme integration |
| **Credential storage** | `~/.gitconfig` (`[credential "https://trgl.gitlab-dedicated.com"]`) | Reuses existing Git credential format |
| **OAuth redirect** | Loopback IP (`http://127.0.0.1:{random port}/callback`) | Temporary `HttpListener`, auto-captures code, no admin needed |
| **OAuth HTTP calls** | `System.Net.Http.HttpClient` | Direct calls to GitLab `/oauth/token` and `/api/v4/user` |
| **Duo integration** | `DirectSetAndSave`: sets on `GeneralSettings.Instance` + `Save()` | Mimics Options dialog OK button. `Save()` triggers full event chain: Duo activates with project auto-selected |
| **Token encryption** | `System.Security.Cryptography.ProtectedData` (DPAPI, CurrentUser) | Matches GitLab extension's `ProtectImpl` format |
| **Auto-refresh** | `System.Windows.Threading.DispatcherTimer` | Fires 5 min before token expiry |
| **JSON parsing** | `System.Text.Json` (NuGet 8.0.5) | Not in net472 BCL |
| **Build** | `dotnet build` (DLL) + VS MSBuild (VSIX) | CLI compiles; VSIX packaging needs VS |

## Key NuGet Packages

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.VisualStudio.SDK` | 17.0.32112.339 | VS Shell, Interop, DTE APIs |
| `Microsoft.VSSDK.BuildTools` | 17.5.4074 | VSIX build targets, VSCT compiler |
| `System.Text.Json` | 8.0.5 | JSON parsing for OAuth responses |

> **Note:** WebView2 has been removed. No `Microsoft.Web.WebView2` dependency.

## GUIDs and Identifiers

| Identifier | Value | Used In |
|---|---|---|
| Package GUID | `b8f3c1a2-4d5e-6f78-9a0b-c1d2e3f4a5b6` | `OAuthTokenGenExtPackage.cs`, `.vsct` |
| Command Set GUID | `c7d8e9f0-1a2b-3c4d-5e6f-708192a3b4c5` | `OpenTokenGeneratorCommand.cs`, `.vsct` |
| Tool Window GUID | `d1e2f3a4-b5c6-7d8e-9f0a-1b2c3d4e5f60` | `TokenGeneratorToolWindow.cs` |
| VSIX Identity | `OAuthTokenGenExt.a1b2c3d4-e5f6-7890-abcd-ef1234567890` | `source.extension.vsixmanifest` |
| Command ID | `0x0100` | `.vsct`, `OpenTokenGeneratorCommand.cs` |
| Menu Group ID | `0x1020` | `.vsct` |

## Data Flow

```
Extension loads
  │
  ├── GitConfigService.ReadCredentials() ──> reads ~/.gitconfig
  │   └── Extracts gitLabDevClientId, gitLabDevClientSecret, URL from [credential "https://..."]
  │   └── Auto-populates GitLab URL, Application ID, Secret fields
  │
  ├── User clicks "Authorize with GitLab" (one click, fully automatic)
  │   ├── LoopbackHttpListener() ──> starts HttpListener on 127.0.0.1:{random port}
  │   └── Process.Start(authorizeUrl with redirect_uri=http://127.0.0.1:{port}/callback)
  │
  ├── User authorizes in GitLab ──> GitLab redirects to 127.0.0.1:{port}/callback?code=XYZ
  │
  ├── LoopbackHttpListener captures code automatically (verifies state, serves success page)
  │
  │
  ├── OAuthTokenService.ExchangeCodeAsync() ──> POST /oauth/token via HttpClient
  │   └── Stores: AccessToken, RefreshToken, ExpiresIn, ExpiresAt
  │
  ├── OAuthTokenService.ValidateAsync() ──> GET /api/v4/user
  │   └── Stores: UserName, UserLogin
  │
  ├── ShowTokenDisplay() ──> shows token in UI with 📋 Copy button
  │
  ├── Dispatcher.InvokeAsync(() => { ... })  ──> ensures UI thread for all below
  │
  ├── GitLabDuoSettingsService.SetTokenAndUrl(token, url) via DirectSetAndSave()
  │   ├── DpapiProtect(token) ──> ProtectedData.Protect(CurrentUser) ──> Base64
  │   ├── GeneralSettings.Instance.AccessToken = encryptedBase64  (via reflection)
  │   ├── GeneralSettings.Instance.GitLabUrl = url                (via reflection)
  │   └── GeneralSettings.Instance.Save()  (mimics Options dialog OK button)
  │       ├── Persists to WritableSettingsStore (with "1*" prefix)
  │       └── Fires Saved event ──> Settings re-reads ──> SettingsChangedEvent
  │           └── LS client sends DidChangeConfiguration (Token + BaseUrl + ProjectPath)
  │               └── Duo Agent activates with project auto-selected, ready for chat
  ├── GitLabDuoSettingsService.GetAccessToken() ──> DpapiUnprotect(read-back) ──> verify (shows ✅/⚠️)
  │
  ├── GitConfigService.WriteCredentials() ──> saves to ~/.gitconfig
  │
  └── StartAutoRefreshTimer()
      └── DispatcherTimer fires at (expiresIn - 300) seconds
          ├── OAuthTokenService.RefreshAsync() ──> POST /oauth/token (grant_type=refresh_token)
          ├── OAuthTokenService.ValidateAsync()
          ├── GitLabDuoSettingsService.SetAccessToken(newToken)
          └── Timer resets for next cycle
```

## Service Classes

| Service | File | Responsibility |
|---|---|---|
| `GitConfigService` | `Services/GitConfigService.cs` | Static class. Reads/writes `[credential "https://..."]` sections in `~/.gitconfig`. Parses `gitLabDevClientId` and `gitLabDevClientSecret` via regex. |
| `OAuthTokenService` | `Services/OAuthTokenService.cs` | Instance class. Holds current token state (AccessToken, RefreshToken, ExpiresAt, UserName). Methods: `ExchangeCodeAsync`, `RefreshAsync`, `ValidateAsync`, `ExtractCode`, `BuildAuthorizeUrl`. Uses `HttpClient`. |
| `LoopbackHttpListener` | `Services/LoopbackHttpListener.cs` | Disposable class. Starts `HttpListener` on `127.0.0.1:{random port}`. `WaitForCallbackAsync(state, timeout)` captures the OAuth redirect, verifies CSRF state, serves a success page, returns the code. Auto-shuts down. |
| `GitLabDuoSettingsService` | `Services/GitLabDuoSettingsService.cs` | Static class. `SetTokenAndUrl()` calls `DirectSetAndSave()` which sets DPAPI-encrypted token and URL directly on `GeneralSettings.Instance` via reflection, then calls `Save()`. This mimics exactly what happens when the user clicks OK in Tools > Options > GitLab. `Save()` persists to the settings store and fires the `Saved` event, triggering the full chain: Settings -> SettingsChangedEvent -> LS client -> DidChangeConfiguration -> Duo Agent activates with project auto-selected. Verifies by decrypting read-back. |

## .gitconfig Format

The extension reads and writes this format:

```ini
[credential "https://trgl.gitlab-dedicated.com"]
    gitLabDevClientId = db846752d4655d6754e5a5ef908962bf01475a4fa6a04ab4c15a4e2e86469d55
    gitLabDevClientSecret = gloas_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
    gitLabAuthModes = browser
    provider = gitlab
```

- The URL in the `[credential "..."]` section becomes the GitLab URL (e.g., `https://trgl.gitlab-dedicated.com`)
- `gitLabDevClientId` becomes the Application ID (client_id)
- `gitLabDevClientSecret` becomes the Application Secret (client_secret)

## Build Outputs

```
dotnet build -c Release
  └── bin\Release\net472\
      ├── OAuthTokenGenExt.dll           # Main extension assembly
      ├── System.Text.Json.dll           # JSON parsing
      ├── System.Buffers.dll             # Dependency
      ├── System.Memory.dll              # Dependency
      └── Resources\icon.png             # Extension icon

Visual Studio Build (Release)
  └── bin\Release\OAuthTokenGenExt.vsix  # Distributable VSIX package
```

## Agent Guidelines

When working on this project:

1. **This targets Visual Studio 2022/2026 IDE**, not VS Code. Uses VSSDK, WPF, DTE, and `ToolWindowPane`.

2. **The UI is native WPF**, not WebView2. The XAML is in `TokenGeneratorControl.xaml`. There is no HTML/CSS/JS bridge. WebView2 has been completely removed.

3. **The target framework is `net472`**. Modern C# features are available via `<LangVersion>latest</LangVersion>` but BCL APIs are limited. `System.Text.Json`, `System.Net.Http`, and `System.Web` are added via NuGet or framework references.

4. **Credentials are stored in `~/.gitconfig`**, not `%APPDATA%` JSON files. The `GitConfigService` reads/writes `[credential "https://..."]` sections with `gitLabDevClientId` and `gitLabDevClientSecret` keys.

5. **Tokens are set via `DirectSetAndSave` (mimics Options dialog OK)**. `SetTokenAndUrl()` sets DPAPI-encrypted token + URL directly on `GeneralSettings.Instance` via reflection, then calls `Save()`. This is the exact same code path as clicking OK in Tools > Options > GitLab. `Save()` persists to the store and fires the `Saved` event, which triggers the full chain ending with Duo Agent activating with the project auto-selected. All post-exchange operations are wrapped in `Dispatcher.InvokeAsync`. The same flow runs on every auto-refresh.

6. **Auto-refresh uses `DispatcherTimer`**. It fires at `expiresIn - 300` seconds (5 min before expiry). On each tick it calls `RefreshAsync`, validates, and re-pushes to Duo settings.

7. **VSIX packaging requires Visual Studio**. `dotnet build` compiles the DLL but the `.vsix` container needs MSBuild from within VS.

8. **No admin access assumption**. Credentials in `~/.gitconfig`, VSIX installs per-user, no ports or servers opened.

9. **The OAuth flow uses a loopback IP redirect** with a temporary `HttpListener` on `127.0.0.1:{random port}`. One click: listener starts, browser opens, code is captured automatically, token is exchanged and pushed to Duo. No manual fallback UI (the loopback listener handles everything).

10. **The UI has 4 sections**: (1) Token Status (always visible: Not Authorized / Active / Expired, with refresh button), (2) Credentials (read-only from .gitconfig, with 👁/🔒 show/hide toggle on the secret), (3) Authorize button, (4) Activity Log. Credentials fields are `IsReadOnly=True`. Refresh button is always visible but disabled until first authorization.
