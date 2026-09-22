# OAuthTokenGenExt - GitLab OAuth Token Generator (Visual Studio Extension)

A **Visual Studio 2022/2026** extension that generates OAuth 2.0 access tokens for the **GitLab for Visual Studio** extension. Runs entirely inside Visual Studio as a native WPF tool window with **no admin access**, **no local server**, and **no Docker** required. Automatically reads credentials from `~/.gitconfig`, pushes tokens directly into GitLab Duo settings, and auto-refreshes every 2 hours.

## The Problem

Setting up GitLab Duo (Code Suggestions, Duo Chat, Code Review) in Visual Studio requires an OAuth access token. The manual process involves constructing authorization URLs, handling browser redirects, extracting tokens, and pasting them into Visual Studio settings. Tokens expire every 2 hours by default, making this a recurring task.

Previous solutions (console apps, standalone executables, web apps) each required running something outside Visual Studio. In locked-down enterprise environments, users often cannot install Docker, open arbitrary ports, or run local web servers.

**This extension solves all of that.** It reads your credentials from `.gitconfig`, generates tokens inside VS, auto-refreshes them before expiry, and pushes them directly into the GitLab Duo extension settings. Zero manual copy-paste after the initial authorization.

## Quick Start

#### Build & Install (from source)

```powershell
cd OAuthTokenGenExt
.\build.ps1
```

#### Build the VSIX Package (command line)

The project uses the `Microsoft.VisualStudio.Sdk.Build` SDK which requires MSBuild from Visual Studio (not `dotnet build`):

```powershell
# Using MSBuild from VS 2022 (produces DLL + VSIX + pkgdef + ctmenu)
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" `
    OAuthTokenGenExt.csproj /p:Configuration=Release /restore /t:Rebuild

# Output: bin\Release\vs17.0\OAuthTokenGenExt.vsix
```

Or build from within Visual Studio:

1. Open `OAuthTokenGenExt.sln` in **Visual Studio 2022 or 2026**
2. Set configuration to **Release**
3. Build: **Ctrl+Shift+B**
4. The `.vsix` file will be in `bin\Release\vs17.0\`

> **Important:** `dotnet build` will **not** work for this project. The `Microsoft.VisualStudio.Sdk.Build` SDK is only available through MSBuild from a Visual Studio installation.

#### Install from VSIX (no admin required)

Double-click the `.vsix` file. The VSIX Installer will prompt to install for your user account.

```powershell
# Or from the command line:
"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\VSIXInstaller.exe" OAuthTokenGenExt.vsix
```

Per-user VSIX extensions install to `%LOCALAPPDATA%\Microsoft\VisualStudio\`. No admin elevation needed.

## Usage

After installing and restarting Visual Studio:

1. Go to **Tools > GitLab OAuth Token Generator**
2. The tool window opens with credentials **auto-populated from `~/.gitconfig`** (if configured)
3. Verify the **GitLab URL**, **Application ID**, and **Secret** are correct
4. Click **🚀 Authorize with GitLab**

That's it. The extension handles everything automatically:
- Starts a **temporary local HTTP listener** on `127.0.0.1` (random port, no admin needed)
- Opens the GitLab authorization page in your default browser
- Waits for GitLab to redirect back to the listener with the authorization code
- Shows a **"Authorization Successful!"** page in the browser
- Exchanges the code for an access token (scope: `api read_user ai_features`)
- Validates the token against `/api/v4/user`
- **Displays the token** with a 📋 Copy button (as a fallback) and creation datetime
- **Sets the token and URL directly on the GitLab extension's in-memory singleton** (`GeneralSettings.Instance`) via reflection, exactly mimicking what happens when you click OK in Tools > Options > GitLab
- The token is **DPAPI-encrypted** before setting (matching the extension's `ProtectImpl` format)
- Calls `GeneralSettings.Instance.Save()` which **persists to the settings store** and **fires the Saved event**
- The Saved event triggers the full downstream chain: Settings re-reads -> LS client sends `DidChangeConfiguration` -> project detected from git remote -> **Duo Agent activates with project selected**
- **Verifies** the token was written by reading it back (decrypts via `DpapiUnprotect`)
- All post-exchange operations run on the **UI thread** via `Dispatcher.InvokeAsync`
- Saves credentials back to `~/.gitconfig`
- Starts an **auto-refresh timer** (refreshes 5 minutes before expiry, re-encrypts and re-applies to Duo automatically)
- Shuts down the listener

**One click. Fully automatic. No copy-paste.**

## How It Works

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│  VS Tool Window  │     │  Default Browser  │     │  GitLab Instance │
│  Click Authorize │────>│  Opens auth page  │────>│  User authorizes │
│                  │     │                  │     │                  │
│  Starts local    │     │                  │     │  Redirects to    │
│  HTTP listener   │     │                  │     │  127.0.0.1:port  │
│  on 127.0.0.1    │     │                  │     │  /callback?code= │
└────────┬─────────┘     └──────────────────┘     └────────┬─────────┘
         │                                                  │
         │◄─────────────────────────────────────────────────┘
         │  Listener captures ?code= automatically
         ▼
┌──────────────────┐     ┌──────────────────┐
│  Exchange code   │────>│  Token pushed to │
│  for token       │     │  GitLab Duo      │
│  POST /oauth/    │     │  settings (auto) │
│  token           │     │  + verified      │
└──────────────────┘     └────────┬─────────┘
                                  │
                         ┌────────▼─────────┐
                         │  Auto-refresh    │
                         │  every ~2 hours  │
                         │  (before expiry) │
                         └──────────────────┘
```

1. On load, the extension reads `gitLabDevClientId` and `gitLabDevClientSecret` from `~/.gitconfig`
2. The user clicks **Authorize with GitLab**
3. The extension starts a **temporary HTTP listener** on `127.0.0.1` with a random port
4. The browser opens the GitLab authorization page with `redirect_uri=http://127.0.0.1:{port}/callback`
5. After the user authorizes, GitLab redirects to the listener, which **captures the code automatically**
6. The browser shows a **"Authorization Successful!"** page
7. The extension exchanges the code for a token via `POST /oauth/token` (scope: `api read_user ai_features`)
8. The token is validated via `GET /api/v4/user`
9. The token is displayed in the UI with creation datetime and a 📋 Copy button
10. Via reflection, `GeneralSettings.Instance.AccessToken` is set to the DPAPI-encrypted token, and `GeneralSettings.Instance.GitLabUrl` is set to the URL (mimicking the Options dialog OK button)
11. `GeneralSettings.Instance.Save()` is called, which persists to the settings store AND fires the `Saved` event
12. The `Saved` event triggers: Settings re-reads -> `SettingsChangedEvent` -> LS client sends `DidChangeConfiguration` (Token + BaseUrl + ProjectPath from git remote) -> **Duo Agent activates with project auto-selected**
13. The push is **verified** by reading and decrypting the value back
14. A `DispatcherTimer` auto-refreshes the token 5 minutes before expiry, repeating steps 10-12 automatically
15. On each auto-refresh, Duo features (Code Suggestions, Duo Chat, Agentic Chat) continue working without interruption
16. The listener shuts down

## .gitconfig Integration

The extension reads and writes OAuth credentials from the user's `~/.gitconfig` file. This means credentials persist across VS sessions and are shared with other Git tools.

#### Expected .gitconfig Format

```ini
[credential "https://trgl.gitlab-dedicated.com"]
    gitLabDevClientId = db846752d4655d6754e5a5ef908962bf01475a4fa6a04ab4c15a4e2e86469d55
    gitLabDevClientSecret = gloas_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
    gitLabAuthModes = browser
    provider = gitlab
```

- On **load**: The extension scans `~/.gitconfig` for `[credential "https://..."]` sections containing `gitLabDevClientId` and auto-populates the form fields
- On **authorize/exchange**: The extension saves the credentials back to `~/.gitconfig` in the same format
- If no credentials are found, the user enters them manually (one-time setup)

## Setting Up the GitLab OAuth Application

Before using the extension, create an OAuth application in your GitLab instance:

1. Navigate to **User Settings > Applications** (or `/-/user_settings/applications`)
2. Click **Add new application**
3. Fill in:

   | Field | Value |
   |---|---|
   | **Name** | `VS OAuth Token Generator` |
   | **Redirect URI** | `http://127.0.0.1:8585/callback` (see note below) |
   | **Confidential** | Yes (checked) |
   | **Scopes** | `api`, `read_user`, `ai_features` (all checked) |

4. Click **Save application** and note the **Application ID** and **Secret**

> **Redirect URI note:** The extension uses a **loopback IP redirect** (`http://127.0.0.1:{port}/callback`). It starts a temporary HTTP listener on a random port, so the exact port varies each time. GitLab allows any port on `127.0.0.1` if the base URI matches. Register `http://127.0.0.1:8585/callback` as the Redirect URI.

## Features

| Feature | Description |
|---|---|
| **One-Click Authorize** | Click once: listener starts, browser opens, code captured automatically, token exchanged |
| **Loopback IP Redirect** | Temporary HTTP listener on `127.0.0.1` (random port). No admin, no firewall rules needed |
| **Auto-Populate from .gitconfig** | Reads `gitLabDevClientId` and `gitLabDevClientSecret` from `~/.gitconfig` on load |
| **Save to .gitconfig** | Writes credentials back to `~/.gitconfig` after successful authorization |
| **Auto-Refresh** | Automatically refreshes the token 5 minutes before expiry (~every 2 hours) |
| **GitLab Duo Integration** | Sets token+URL directly on `GeneralSettings.Instance` via reflection (mimics Options dialog OK), calls `Save()` to trigger the full event chain. Duo Chat, Code Suggestions, Agentic Chat activate immediately with project auto-selected |
| **Duo Settings Verification** | Reads back the token after setting it to confirm it was applied |
| **Token Display + Copy** | Shows the current token with a 📋 Copy button |
| **Token Datetime** | Shows creation time and expiry time (e.g., "Created: 2026-09-16 01:15:23", "Expires in 1h 55m (at 03:10:23)") |
| **Refresh Button** | 🔄 always visible, disabled until first authorization, then enabled for manual refresh |
| **Status Banner (always visible)** | Shows "Not Authorized" / "Token Active" / "Token Expired" with user, datetime, and expiry countdown |
| **Read-only Credentials** | GitLab URL, Application ID, Secret are read-only, auto-loaded from `~/.gitconfig` |
| **Secret Show/Hide** | 👁/🔒 toggle button next to the Application Secret field to reveal or hide the value |
| **Activity Log** | Timestamped log of all operations (authorize, exchange, refresh, errors) |
| **CSRF Protection** | Random `state` parameter on every authorization request |
| **No Admin Required** | Per-user VSIX install, credentials in `~/.gitconfig` |
| **Native WPF UI** | Pure WPF form (no WebView2 dependency) |

## Project Structure

```
OAuthTokenGenExt/
├── OAuthTokenGenExt.csproj              # VS SDK project (net472, VSSDK, System.Text.Json)
├── OAuthTokenGenExt.sln                 # Solution file for VS IDE
├── source.extension.vsixmanifest        # VSIX manifest (VS 2022 + 2026 targets)
├── OAuthTokenGenExtPackage.cs           # AsyncPackage entry point
├── OAuthTokenGenExtPackage.vsct         # Command table (Tools menu item)
├── OpenTokenGeneratorCommand.cs         # MenuCommand handler
├── TokenGeneratorToolWindow.cs          # ToolWindowPane definition
├── TokenGeneratorControl.xaml           # WPF UI (Status, Credentials, Authorize, Log)
├── TokenGeneratorControl.xaml.cs        # OAuth flow, auto-refresh, Duo integration
├── Services/
│   ├── GitConfigService.cs              # Read/write ~/.gitconfig credentials
│   ├── OAuthTokenService.cs             # Token exchange, refresh, validation via HttpClient
│   ├── LoopbackHttpListener.cs          # Temporary HTTP listener on 127.0.0.1 for OAuth callback
│   └── GitLabDuoSettingsService.cs      # Write to VS settings store + force GitLab extension reload via reflection
├── Properties/
│   └── launchSettings.json              # F5 debug profiles (VS 2022/2026 Experimental Instance)
├── build.ps1                            # Build & install script
├── AGENTS.md                            # AI agent context
├── DESIGN.md                            # Architecture and design decisions
├── README.md                            # This file
└── Resources/
    └── icon.png                         # Extension icon
```

## Architecture

```
Visual Studio Process
  └── OAuthTokenGenExtPackage (AsyncPackage)
        └── TokenGeneratorToolWindow (ToolWindowPane)
              └── TokenGeneratorControl (native WPF UserControl)
                    ├── GitConfigService ──> ~/.gitconfig (read/write credentials)
                    ├── OAuthTokenService ──> GitLab Instance (HttpClient)
                    │     ├── POST /oauth/token (exchange + refresh)
                    │     └── GET /api/v4/user (validation)
                    ├── GitLabDuoSettingsService.SetTokenAndUrl()
                    │     ├── GeneralSettings.Instance.AccessToken = DpapiProtect(token)
                    │     ├── GeneralSettings.Instance.GitLabUrl = url
                    │     └── GeneralSettings.Instance.Save()
                    │           ├── Persists to WritableSettingsStore
                    │           └── Fires Saved event ──> Settings ──> LS client ──> Duo activates
                    └── DispatcherTimer ──> auto-refresh (every expiresIn - 300 seconds)
```

#### Dependencies

| Dependency | Source | Purpose |
|---|---|---|
| `Microsoft.VisualStudio.Sdk.Build` | Project SDK (`global.json`: 17.5.4074) | VSSDK build pipeline: VSCT compiler, pkgdef generator, VSIX packager, VS SDK references |
| `System.Text.Json` | NuGet 8.0.5 | JSON parsing for OAuth responses (not in net472 BCL) |
| `System.Net.Http` | Framework reference | `HttpClient` for OAuth API calls |
| `System.Web` | Framework reference | `HttpUtility.ParseQueryString` for URL parsing |
| `PresentationFramework` | Framework reference | WPF controls (TextBox, Button, StackPanel, etc.) |

> **Note:** The `Microsoft.VisualStudio.Sdk.Build` SDK automatically provides all VS SDK NuGet packages (Shell, Interop, DTE, VSSDK.BuildTools). No separate `PackageReference` for these is needed.

## Token Expiration and Auto-Refresh

OAuth access tokens expire based on the GitLab server configuration (default: **2 hours / 7200 seconds**).

The extension handles this automatically:

1. After generating a token, a `DispatcherTimer` is started
2. The timer fires **5 minutes before the token expires** (e.g., at 1h 55m for a 2-hour token)
3. On each tick, the extension calls `POST /oauth/token` with `grant_type=refresh_token`
4. The new token is validated and **automatically pushed** to GitLab Duo settings
5. The timer resets for the next refresh cycle

The status banner shows:
- **✅ Token Active** with "Expires in Xh Ym" countdown (updates every 30 seconds)
- **🔄 Auto-refresh active (every Xh Ym)** in a blue banner
- **⏱ Token Expired** if the refresh fails

Manual refresh is always available via the **🔄 Refresh Token Now** button.

Admins can increase the token lifetime on self-managed instances:

```ruby
# /etc/gitlab/gitlab.rb
gitlab_rails['doorkeeper_access_token_expires_in'] = 86400  # 24 hours
```

## Security

- **No intermediary server**: All OAuth API calls go directly from the extension to your GitLab instance via `HttpClient`
- **CSRF protection**: A cryptographically random `state` parameter is generated for each authorization request
- **Credentials in .gitconfig**: Stored in the user's home directory, same location Git itself uses
- **Per-user install**: VSIX installs to `%LOCALAPPDATA%\Microsoft\VisualStudio\`, no admin elevation
- **No telemetry**: Zero analytics, tracking, or external service calls
- **Token DPAPI-encrypted**: The token is encrypted with `ProtectedData.Protect(CurrentUser)` before storage, matching the GitLab extension's own encryption. The raw token is shown in the UI with a Copy button but is never stored unencrypted on disk

## Troubleshooting

| Issue | Solution |
|---|---|
| **VSIX won't install** | Ensure VS 2022 (17.0+) or VS 2026 is installed. Try double-clicking the `.vsix` file |
| **Credentials not auto-populated** | Check `~/.gitconfig` has a `[credential "https://trgl.gitlab-dedicated.com"]` section with `gitLabDevClientId` |
| **"GitLab extension not found"** | Install the "GitLab for Visual Studio" extension from Extensions > Manage Extensions |
| **Token not applied to Duo settings** | Check the DuoSettingsStatus line below the token. If it shows ⚠️, use the 📋 Copy button and paste manually into Tools > Options > GitLab > General > Access Token |
| **"Authentication required" in Duo Chat** | The token may not be DPAPI-encrypted correctly. Re-authorize. If persists, copy the token manually into Tools > Options > GitLab |
| **Duo Agentic Chat: no project selected** | The git remote URL must match the GitLabUrl. Check `git remote -v` points to your GitLab instance. Close and reopen the Duo Chat window after authorization |
| **Auto-refresh not working** | The refresh token may have expired. Re-authorize using the Generate flow |
| **GitLab shows error after authorize** | Ensure `http://127.0.0.1:8585/callback` is registered as a Redirect URI in your GitLab OAuth app |
| **Listener timeout (5 min)** | The listener waits 5 minutes for the callback. If it times out, click Authorize again |
| **"I don't have access to the file"** | Ensure files are pushed, OAuth app has `api` scope, user has Reporter+ access |
| **`dotnet build` fails** | This project uses `Microsoft.VisualStudio.Sdk.Build` SDK. Use MSBuild from VS instead (see Building from Source) |

## Building from Source

#### Prerequisites

| Requirement | Details |
|---|---|
| **Visual Studio 2022** (17.0+) or **2026** (18.x) | Any edition. Required for MSBuild, VSCT compiler, and F5 debugging |
| **MSBuild** | Included with VS at `C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe` |

> **Note:** `dotnet build` will **not** work for this project. The `Microsoft.VisualStudio.Sdk.Build` SDK is resolved through MSBuild from a Visual Studio installation, not the .NET CLI.

#### Option 1: Build from Command Line (DLL + VSIX)

```powershell
cd OAuthTokenGenExt

# MSBuild from VS 2022 (produces DLL + VSIX + pkgdef + ctmenu)
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" `
    OAuthTokenGenExt.csproj /p:Configuration=Debug /restore /t:Rebuild

# Output: bin\Debug\vs17.0\OAuthTokenGenExt.vsix
```

For Release:
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" `
    OAuthTokenGenExt.csproj /p:Configuration=Release /restore /t:Rebuild
```

#### Option 2: Build from Visual Studio IDE

1. Open `OAuthTokenGenExt.sln` in Visual Studio 2022 or 2026
2. Set configuration to **Debug** or **Release** (toolbar dropdown)
3. Build: **Build > Rebuild Solution** (or **Ctrl+Shift+B**)
4. The `.vsix` file will be in `bin\Debug\vs17.0\` or `bin\Release\vs17.0\`

#### Option 3: Build Script

```powershell
cd OAuthTokenGenExt
.\build.ps1                    # Build + install
.\build.ps1 -BuildOnly         # Build only
```

#### Build Output

```
bin\Debug\vs17.0\
├── OAuthTokenGenExt.dll        # Main extension assembly (46KB)
├── OAuthTokenGenExt.pkgdef     # VS package registration (menu, tool window)
├── OAuthTokenGenExt.vsix       # Distributable VSIX package (53KB)
├── System.Text.Json.dll        # JSON parsing (NuGet)
├── Resources\icon.png          # Extension icon
└── (other System.* dependencies)
```

## Testing Locally

#### F5 Debugging (Experimental Instance)

This launches a second Visual Studio instance with your extension loaded:

1. Open `OAuthTokenGenExt.sln` in **Visual Studio 2022**
2. Set configuration to **Debug**
3. Right-click the project > **Properties** > **Debug**
4. Set **Start external program** to:
   ```
   C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe
   ```
5. Set **Command line arguments** to:
   ```
   /rootsuffix Exp
   ```
6. Press **F5** (or **Debug > Start Debugging**)

What happens:
- VS builds the extension and deploys it to the **Experimental Instance**
- A second VS window opens with `[Experimental Instance]` in the title bar
- In that window, go to **Tools > GitLab OAuth Token Generator**
- The tool window appears with the native WPF UI

> **Tip:** The `Properties\launchSettings.json` file has pre-configured profiles for both VS 2022 and VS 2026.

#### Testing the Full Flow

1. **Verify .gitconfig loading**: Open the tool window. The Activity Log should show "GitLab URL loaded from .gitconfig" if your `~/.gitconfig` has credentials
2. **Test authorization**: Click **Authorize with GitLab**. Your browser should open the GitLab authorization page
3. **Verify token display**: After authorizing, the token should appear with a 📋 Copy button and ✅ green status
4. **Verify Duo integration**: The DuoSettingsStatus line should show ✅ "Token applied to GitLab Duo settings". Check **Tools > Options > GitLab > General** to confirm
5. **Verify status banner**: Should show "Token Active" with authenticated user, creation datetime, and expiry countdown
5. **Verify auto-refresh**: The blue "Auto-refresh active" banner should appear with the refresh interval
6. **Test manual refresh**: Click the **🔄 Refresh Token Now** button

#### Installing the VSIX Locally

```powershell
# Close all Visual Studio instances first, then:

# VS 2022
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\VSIXInstaller.exe" `
    "OAuthTokenGenExt\bin\Release\vs17.0\OAuthTokenGenExt.vsix"

# VS 2026
& "C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\VSIXInstaller.exe" `
    "OAuthTokenGenExt\bin\Release\vs17.0\OAuthTokenGenExt.vsix"

# Or just double-click the .vsix file
```

#### Uninstalling

1. In Visual Studio: **Extensions > Manage Extensions > Installed**
2. Find **GitLab OAuth Token Generator** and click **Uninstall**
3. Restart Visual Studio

#### Resetting the Experimental Instance

If the Experimental Instance gets into a bad state:

```powershell
# VS 2022
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\VSSDK\VisualStudioIntegration\Tools\Bin\CreateExpInstance.exe" `
    /Reset /VSInstance=17.0 /RootSuffix=Exp
```

#### Verifying the Extension Loaded

After installing or F5 debugging:
- **Tools menu** should show **GitLab OAuth Token Generator**
- **Extensions > Manage Extensions > Installed** should list the extension
- Opening the tool window should show the WPF form with credential fields and the Activity Log

#### Troubleshooting Build Issues

| Issue | Solution |
|---|---|
| `dotnet build` fails with "SDK not found" | Expected. This project uses `Microsoft.VisualStudio.Sdk.Build`. Use MSBuild from VS: `& "...\MSBuild.exe" OAuthTokenGenExt.csproj /restore` |
| F5 doesn't launch Experimental Instance | Check Debug properties: Start external program = `devenv.exe`, args = `/rootsuffix Exp` |
| "Tools > GitLab OAuth Token Generator" missing | The VSCT was not compiled. You must build with MSBuild from VS (not `dotnet build`). The `Menus.ctmenu` resource must be embedded in the DLL |
| Changes not reflected after rebuild | Clean first: **Build > Clean Solution**, then Rebuild. Or delete `bin\` and `obj\` manually |
| Extension loads but UI is empty | Check the Output window (Debug) for XAML binding errors |
| Build output is in `bin\Debug\vs17.0\` not `net472\` | Correct. The `vs17.0` target framework is used by the VSSDK SDK (maps to net472 internally) |

## Origin and Version History

| Version | Type | Key Change |
|---|---|---|
| **V1** | .NET Console App | First automation of the OAuth flow |
| **V2** | Standalone EXE | No .NET SDK required, token refresh, JSON storage |
| **V3** | Static Web App | Browser-based 4-tab UI, Docker, GitLab Pages |
| **V4** | **VS Extension** | **Native WPF, .gitconfig integration, auto-refresh, Duo settings push** |

## References

- [GitLab OAuth 2.0 Documentation](https://docs.gitlab.com/ee/api/oauth2.html)
- [GitLab Duo Code Suggestions Setup](https://docs.gitlab.com/ee/user/project/repository/code_suggestions/set_up.html)
- [Visual Studio GitLab Extension](https://marketplace.visualstudio.com/items?itemName=GitLab.GitLabExtensionForVisualStudio)
- [VSSDK Extensibility Documentation](https://learn.microsoft.com/en-us/visualstudio/extensibility/starting-to-develop-visual-studio-extensions)
