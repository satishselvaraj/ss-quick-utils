# OAuthTokenGenExt - Design Document

## Overview

OAuthTokenGenExt is a Visual Studio 2022/2026 extension that generates OAuth 2.0 access tokens for the **GitLab for Visual Studio** extension. It was created to eliminate the need for external servers, Docker, or browser-based tools by embedding the entire token lifecycle directly inside the IDE.

The extension reads credentials from `~/.gitconfig`, generates and refreshes tokens automatically, and pushes them directly into the GitLab Duo extension settings via the DTE automation API. It requires **no admin access**, **no local server**, and **no manual copy-paste** after the initial authorization.

---

## How It Was Built

#### Step 1: Scaffolding the VS SDK Project

The project was scaffolded using the official Microsoft VSIX template:

```powershell
dotnet new install Microsoft.VisualStudio.Sdk.Templates
dotnet new vsix -n OAuthTokenGenExt -o OAuthTokenGenExt
```

The generated `Microsoft.VisualStudio.Sdk.Build` SDK project was migrated to a standard `Microsoft.NET.Sdk` project targeting `net472` for broader CLI compatibility.

#### Step 2: Choosing the Extensibility Model

| Model | Pros | Cons |
|---|---|---|
| **VSSDK (in-process)** | Mature, full DTE API access, VS 2022 + 2026, WPF tool windows | Runs in VS process, .NET Framework 4.7.2 |
| **VisualStudio.Extensibility (out-of-process)** | Modern, isolated, .NET 8+ | Limited API, no DTE access for settings push, VS 2022 17.9+ only |

**Decision:** VSSDK (in-process) was chosen because it provides DTE automation access needed to push tokens into the GitLab extension's settings (`Tools > Options > GitLab > General`), and supports native WPF tool windows across both VS 2022 and VS 2026.

#### Step 3: Native WPF UI (replacing WebView2)

The initial prototype used WebView2 to host an HTML/CSS/JS UI adapted from the V3 web app. This was replaced with a **native WPF form** for several reasons:

1. **Fewer dependencies**: Removing WebView2 eliminates the `Microsoft.Web.WebView2` NuGet package and the native `WebView2Loader.dll` runtime
2. **Simpler architecture**: No C#/JS bridge, no embedded HTML resources, no `postMessage` protocol
3. **Better VS integration**: Native WPF controls inherit VS theme colors and respond to VS DPI scaling
4. **Single-view design**: The multi-tab UI was simplified to a single "Generate" view since settings are read from `.gitconfig` and tokens are pushed to Duo automatically

The UI consists of a single scrollable `StackPanel` with: status banner, auto-refresh indicator, credential inputs, authorize/exchange buttons, and an activity log.

#### Step 4: .gitconfig Integration

Rather than storing credentials in `%APPDATA%` JSON files or VS settings, the extension reads and writes to `~/.gitconfig` using the same format that the GitLab Credential Manager uses:

```ini
[credential "https://trgl.gitlab-dedicated.com"]
    gitLabDevClientId = db846752d4655d6754e5a5ef908962bf01475a4fa6a04ab4c15a4e2e86469d55
    gitLabDevClientSecret = gloas_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

The `GitConfigService` class parses `.gitconfig` using regex to find `[credential "https://..."]` sections and extract `gitLabDevClientId` / `gitLabDevClientSecret` values. On save, it either updates an existing section or appends a new one.

#### Step 5: OAuth Flow (Loopback IP Redirect, fully automatic)

The OAuth flow went through three iterations:
1. **localhost:8585** (required a persistent server, port conflicts)
2. **OOB mode** (`urn:ietf:wg:oauth:2.0:oob`, required manual code copy-paste, deprecated by some providers)
3. **Loopback IP redirect** (current, recommended for desktop/CLI apps)

The current approach uses a **temporary HTTP listener** on `127.0.0.1` with a random port. The `LoopbackHttpListener` class:
1. Finds a free port by binding a `TcpListener` to port 0 and reading the assigned port
2. Starts an `HttpListener` on `http://127.0.0.1:{port}/`
3. Waits for a single request to `/callback?code=...&state=...`
4. Verifies the `state` parameter (CSRF protection)
5. Serves a styled "Authorization Successful!" HTML page to the browser
6. Returns the authorization code to the caller
7. Shuts down

No admin access or firewall rules are needed for loopback addresses (`127.0.0.1`). The listener lives only for the duration of the authorization flow (max 5 minutes timeout).

```
OnAuthorizeClick()
     │
     ├── LoopbackHttpListener() ──> finds free port, starts HttpListener on 127.0.0.1:{port}
     │
     ├── Process.Start(authorizeUrl with redirect_uri=http://127.0.0.1:{port}/callback)
     │                                                     │
     │                                              GitLab /oauth/authorize
     │                                                     │
     │                                              User authorizes in browser
     │                                                     │
     │                                              GitLab redirects to 127.0.0.1:{port}/callback?code=XYZ
     │                                                     │
     ├── listener.WaitForCallbackAsync() <─────────────────┘
     │   ├── Verifies state parameter
     │   ├── Serves "Authorization Successful!" page to browser
     │   └── Returns authorization code
     │
     ├── ExchangeAndApplyTokenAsync()
     │   ├── OAuthTokenService.ExchangeCodeAsync() ──> POST /oauth/token
     │   ├── OAuthTokenService.ValidateAsync() ──> GET /api/v4/user
     │   ├── ShowTokenDisplay() ──> shows token with Copy button
     │   ├── PushTokenToDuoSettings() ──> DTE + verify by read-back
     │   └── StartAutoRefreshTimer()
     │
     └── listener.Dispose() ──> stops HttpListener
```

#### Step 6: Auto-Refresh Timer

After a successful token exchange, a `DispatcherTimer` is started that fires **5 minutes before the token expires**:

```csharp
var interval = _tokenService.ExpiresIn > 300
    ? TimeSpan.FromSeconds(_tokenService.ExpiresIn - 300)  // 5 min before expiry
    : TimeSpan.FromMinutes(115);                            // fallback: ~2 hours

_refreshTimer = new DispatcherTimer { Interval = interval };
_refreshTimer.Tick += async (_, __) => await RefreshTokenAsync();
_refreshTimer.Start();
```

On each tick:
1. Calls `POST /oauth/token` with `grant_type=refresh_token`
2. Validates the new token via `GET /api/v4/user`
3. Pushes the new token to GitLab Duo settings via DTE
4. Updates the status banner and countdown display

A separate 30-second `DispatcherTimer` updates the "Expires in Xh Ym" countdown in the UI.

#### Step 7: GitLab Duo Settings Integration (WritableSettingsStore + Reflection Reload)

The initial approach used `DTE.Properties["GitLab", "General"]` to set the token, but this failed because the GitLab extension uses `Community.VisualStudio.Toolkit`'s `BaseOptionModel<GeneralSettings>` pattern, which stores settings in the VS `WritableSettingsStore` (private registry), not via DTE automation properties.

The working approach is a three-step process:

**Step 1: DPAPI-encrypt the token**

The GitLab extension's `ProtectImpl` class uses DPAPI (`System.Security.Cryptography.ProtectedData`) to encrypt the `AccessToken` before storage. When it reads the token back, it calls `Unprotect`. If we store the raw token, `Unprotect` fails and Duo reports "Authentication required - token is invalid."

```csharp
// Encrypt the token the same way the GitLab extension does
var plainBytes = Encoding.UTF8.GetBytes(token);
var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
var protectedToken = Convert.ToBase64String(encryptedBytes);
```

**Step 2: Set properties on the in-memory singleton and call Save() (mimics Options dialog OK)**

This is the key insight. Rather than writing to the `WritableSettingsStore` directly and then trying to force a reload, we do exactly what the Options dialog does when the user clicks OK:

```csharp
// Find the GeneralSettings singleton via reflection
var asm = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "GitLab.Extension");
var instance = asm.GetType("GitLab.Extension.SettingsUtil.GeneralSettings")
    .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
    .GetValue(null);

// Set properties directly on the singleton (same as Options dialog binding)
instance.GetType().GetProperty("AccessToken").SetValue(instance, protectedToken);
instance.GetType().GetProperty("GitLabUrl").SetValue(instance, gitlabUrl);

// Save() does TWO things:
//   a) Writes all properties to WritableSettingsStore (persistent, with "1*" prefix)
//   b) Fires the static Saved event
instance.GetType().GetMethod("Save").Invoke(instance, null);
```

The `Save()` call triggers the full downstream event chain:
1. `GeneralSettings.Saved` event fires
2. The `Settings` class (DI singleton) subscribes to `Saved`, re-reads from `GeneralSettings`
3. `Settings.GitLabAccessToken` getter calls `_protect.Unprotect()` on the DPAPI-encrypted value, recovering the raw token
4. `Settings` fires `SettingsChangedEvent`
5. The LS client subscribes to `SettingsChangedEvent`, calls `SettingsToDidChangeConfig()` which builds a config with `Token`, `BaseUrl`, and `ProjectPath` (resolved from the git remote URL matching the `GitLabUrl`)
6. The LS client sends `DidChangeConfiguration` to the Language Server
7. The Language Server authenticates with the token and resolves the project
8. **Duo Agent activates with the project auto-selected, ready for chat**

This approach is reliable because it's the exact same code path that runs when a user manually configures the token through the UI. No separate `WritableSettingsStore` writes, no `Load()` calls, no race conditions.

#### Step 3b: UI thread marshalling

All post-exchange operations (token display, Duo settings push, auto-refresh timer creation) are wrapped in `await Dispatcher.InvokeAsync(() => { ... })`. This is critical because:
- After `await` on HTTP calls, the continuation may resume on a thread pool thread
- `Package.GetGlobalService()` (used by `WritableSettingsStore`) requires the VS main thread
- `DispatcherTimer` must be created on the UI thread
- WPF control updates require the UI thread

The UI shows a status line based on whether `SetTokenAndUrl` succeeded:
- **✅ Green**: "Token applied to GitLab Duo settings. Duo Agent should activate automatically."
- **⚠️ Red**: "Auto-apply failed. Use the Copy button and paste manually."

The token is displayed in a read-only `TextBox` with a 📋 Copy button. The status banner (always visible) shows the token creation datetime and expiry countdown.

The same process runs on every **auto-refresh** (every ~2 hours), ensuring Duo features remain active as long as VS is open.

#### UI Layout

The tool window is organized into four sections:

1. **Token Status** (always visible): Status banner (Not Authorized / Token Active / Token Expired), auto-refresh indicator, token display with Copy button, Refresh button (disabled until first auth)
2. **Credentials** (read-only): GitLab URL, Application ID, Secret. Auto-loaded from `~/.gitconfig`. Fields are read-only with hint text. Secret has a 👁/🔒 show/hide toggle button.
3. **Authorize**: Single button + listening indicator. One click triggers the full flow.
4. **Activity Log**: Timestamped log of all operations.

#### Step 8: .NET Framework 4.7.2 Constraints

| Constraint | Solution |
|---|---|
| No `System.Text.Json` | Added NuGet package `System.Text.Json 8.0.5` |
| No `HttpClient` in default references | Added `<Reference Include="System.Net.Http" />` |
| No `System.Web.HttpUtility` | Added `<Reference Include="System.Web" />` |
| No `ProtectedData` (DPAPI) | Added `<Reference Include="System.Security" />` |
| C# 7.3 default language version | Set `<LangVersion>latest</LangVersion>` |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Visual Studio 2022/2026                       │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  OAuthTokenGenExtPackage (AsyncPackage)                   │   │
│  │  ├── Registers: Tools > GitLab OAuth Token Generator      │   │
│  │  └── Provides: TokenGeneratorToolWindow                   │   │
│  └──────────────────────────────────────────────────────────┘   │
│           │                                                      │
│  ┌────────▼─────────────────────────────────────────────────┐   │
│  │  TokenGeneratorToolWindow (ToolWindowPane)                │   │
│  │  └── Content: TokenGeneratorControl (native WPF)          │   │
│  └──────────────────────────────────────────────────────────┘   │
│           │                                                      │
│  ┌────────▼─────────────────────────────────────────────────┐   │
│  │  TokenGeneratorControl.xaml.cs                            │   │
│  │  ├── OnLoaded: GitConfigService.ReadCredentials()         │   │
│  │  ├── OnAuthorize: Process.Start(authorizeUrl)             │   │
│  │  ├── OnExchange: OAuthTokenService.ExchangeCodeAsync()    │   │
│  │  ├── OnRefresh: OAuthTokenService.RefreshAsync()          │   │
│  │  ├── PushTokenToDuoSettings: GitLabDuoSettingsService     │   │
│  │  ├── DispatcherTimer: auto-refresh (expiresIn - 300s)     │   │
│  │  └── DispatcherTimer: countdown display (every 30s)       │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
    │              │                    │
    ▼              ▼                    ▼
┌─────────┐  ┌──────────────┐  ┌────────────────────────────┐
│~/.git   │  │GitLab        │  │WritableSettingsStore           │
│ config  │  │Instance      │  │  DPAPI Protect(AccessToken)   │
│(r/w)    │  │/oauth/token  │  │  SetString(GitLabUrl)         │
│         │  │/api/v4/user  │  │+ Reflection: Load() + Save()  │
│         │  │              │  │  ──> Duo Chat/Suggestions/     │
│         │  │              │  │      Agentic Chat activate     │
└─────────┘  └──────────────┘  └────────────────────────────────┘
```

---

## File Responsibilities

| File | Role |
|---|---|
| `OAuthTokenGenExt.csproj` | Project: net472, VSSDK, System.Text.Json, framework refs (System.Web, System.Net.Http, System.Design) |
| `source.extension.vsixmanifest` | VSIX metadata: VS 2022-2026 targets, publisher, description |
| `OAuthTokenGenExtPackage.cs` | `AsyncPackage`: registers command and tool window on VS startup |
| `OAuthTokenGenExtPackage.vsct` | Command table: defines Tools menu item with GUID/ID |
| `OpenTokenGeneratorCommand.cs` | `MenuCommand`: opens/shows the tool window |
| `TokenGeneratorToolWindow.cs` | `ToolWindowPane`: sets caption, hosts WPF control |
| `TokenGeneratorControl.xaml` | WPF XAML (VS-themed): 4 sections (Status, Credentials read-only, Authorize, Log). Status banner always visible. |
| `TokenGeneratorControl.xaml.cs` | Core logic: .gitconfig load, one-click OAuth flow, token display with datetime, auto-refresh timer, DirectSetAndSave for Duo |
| `Services/GitConfigService.cs` | Reads/writes `gitLabDevClientId` and `gitLabDevClientSecret` from `~/.gitconfig` |
| `Services/OAuthTokenService.cs` | `HttpClient`-based OAuth token exchange, refresh, and validation |
| `Services/LoopbackHttpListener.cs` | Temporary `HttpListener` on `127.0.0.1:{random port}` for OAuth callback capture |
| `Services/GitLabDuoSettingsService.cs` | `SetTokenAndUrl()` via `DirectSetAndSave()`: sets DPAPI-encrypted token + URL on `GeneralSettings.Instance`, calls `Save()` (mimics Options dialog OK). Triggers full event chain to activate Duo with project auto-selected |
| `Resources/icon.png` | 32x32 extension icon |
| `build.ps1` | PowerShell: `dotnet build` + optional `VSIXInstaller.exe` install |

---

## Key Design Decisions

| Decision | Rationale |
|---|---|
| **Native WPF over WebView2** | Fewer dependencies, no JS bridge, better VS theme integration, simpler architecture |
| **Single Generate view over multi-tab** | Settings come from .gitconfig, tokens push to Duo automatically; no need for separate tabs |
| **~/.gitconfig over %APPDATA% JSON** | Reuses existing Git credential storage, shared with Git CLI and credential managers |
| **DirectSetAndSave mimics Options dialog** | Sets properties on `GeneralSettings.Instance` + calls `Save()`, exactly like clicking OK in Tools > Options > GitLab. Triggers full event chain: Duo activates with project selected |
| **Dispatcher.InvokeAsync for post-exchange** | All VS service calls and UI updates after async HTTP calls are marshalled to the UI thread to prevent threading errors |
| **Auto-refresh via DispatcherTimer** | Tokens stay valid as long as VS is open; no user intervention after initial auth |
| **Token shown with Copy button** | Fallback for manual paste if DTE auto-apply fails; always accessible |
| **Duo settings verification** | Reads back the token after setting it to confirm the write succeeded |
| **Loopback IP redirect** | Temporary `HttpListener` on `127.0.0.1` with random port; no admin, no firewall, fully automatic |
| **One-click flow** | User clicks Authorize once; listener captures code, exchanges token, pushes to Duo, starts auto-refresh |
| **VSSDK over VisualStudio.Extensibility** | DTE access for settings push, VS 2022 + 2026 compatibility |
| **net472** | Required by VSSDK in-process model |

---

## Security Model

1. **No intermediary server**: All OAuth API calls go directly from `HttpClient` to the GitLab instance
2. **CSRF protection**: A cryptographically random `state` parameter (`RandomNumberGenerator`) is generated per authorization
3. **Credentials in .gitconfig**: Same location Git itself uses; user-scoped, not world-readable
4. **Token DPAPI-encrypted at rest**: Encrypted with `ProtectedData.Protect(CurrentUser)` before storage, matching the GitLab extension's own `ProtectImpl`. Raw token shown in UI with Copy button but never stored unencrypted on disk
5. **No telemetry**: Zero analytics, tracking, or external service calls
6. **Per-user install**: VSIX installs to `%LOCALAPPDATA%\Microsoft\VisualStudio\`

---

## Build Pipeline

```
Source Code
    │
    ├── dotnet build (CLI) or MSBuild (VS 2022)
    │   └── Compiles DLL + copies dependencies to bin\Release\net472\
    │       ├── OAuthTokenGenExt.dll
    │       ├── System.Text.Json.dll
    │       └── (framework refs: System.Net.Http, System.Web, System.Design)
    │
    └── MSBuild via Visual Studio (IDE)
        └── Compiles DLL + packages VSIX
            └── OAuthTokenGenExt.vsix
                ├── extension.vsixmanifest
                ├── OAuthTokenGenExt.dll
                ├── OAuthTokenGenExt.pkgdef
                ├── System.Text.Json.dll + dependencies
                └── Resources\icon.png
```

> **Note:** `dotnet build` compiles the project but does not produce the `.vsix` container. VSIX packaging requires building from within Visual Studio because the VSCT compiler is part of the VS MSBuild toolchain.
