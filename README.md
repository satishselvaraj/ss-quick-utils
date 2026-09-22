# SS-Quick-Utils

A collection of utilities for automating GitLab OAuth token generation for the Visual Studio GitLab extension (Duo Chat, Code Suggestions, Code Review). This project contains four versions of the OAuth Token Generator, each building on the previous one.

## The Problem

Setting up GitLab Duo in Visual Studio requires an OAuth access token. The manual process involves constructing authorization URLs, handling browser redirects, extracting tokens from URL parameters, and pasting them into Visual Studio settings. This is error-prone and tokens expire every 2 hours by default.

**This project automates the entire flow.**

## Project Structure

```
SS-quick-utils/
├── OAuthTokenGen/          # V1 - .NET Console Application
├── OAuthTokenGenV2/        # V2 - Standalone Windows Executable
├── OAuthTokenGenV3/        # V3 - Web App (Docker + GitLab Pages)
├── OAuthTokenGenExt/       # V4 - Visual Studio 2022/2026 Extension (VSIX)
├── AGENTS.md               # AI agent context for the project
├── .gitlab-ci.yml          # CI/CD pipeline (build + GitLab Pages)
├── dotnetcore.csproj       # Root .NET project (template)
├── Program.cs              # Root program (template)
└── README.md               # This file
```

## Version Comparison

| Feature | V1 (Console) | V2 (Standalone EXE) | V3 (Web App) | V4 (VS Extension) |
|---|:---:|:---:|:---:|:---:|
| OAuth Flow | Implicit Grant | Authorization Code | Authorization Code | Authorization Code |
| Token Refresh | No | Yes | Yes | Yes |
| Token Validation | Yes | Yes | Yes | Yes |
| Requires .NET SDK | Yes | No | No | No (to install) |
| UI | CLI | CLI (colored) | Web (4 tabs) | Tool Window (4 tabs) |
| Token Storage | None | JSON file | localStorage | %APPDATA% JSON |
| Export/Import | No | No | Yes | Yes |
| Docker Support | No | No | Yes | No (not needed) |
| GitLab Pages | No | No | Yes | No (not needed) |
| Platform | Any (.NET) | Windows x64 | Any browser | VS 2022/2026 |
| Clipboard Copy | Yes | Yes | Yes | Yes |
| VS Setup Guide | Console output | Console output | Interactive tab | Interactive tab |
| Admin Required | No | No | Maybe (Docker) | No |
| IDE Integration | None | None | None | Dockable tool window |

---

## V1: OAuthTokenGen (Console Application)

The initial version. A .NET 8.0 console application that generates OAuth tokens using an interactive CLI.

#### How to Run
```powershell
cd OAuthTokenGen
dotnet restore
dotnet run
```

#### Features
- Two modes: Automatic (local HTTP server captures redirect) and Manual (paste URL)
- Token validation via `GET /api/v4/user`
- Clipboard copy via PowerShell

#### Project Structure
```
OAuthTokenGen/
├── OAuthTokenGen.csproj
├── Program.cs
├── README.md
├── Models/
│   ├── OAuthConfig.cs          # OAuth configuration & URL builder
│   └── TokenResult.cs          # Token result model
└── Services/
    ├── BrowserLauncher.cs      # Cross-platform browser opener
    ├── LocalCallbackServer.cs  # Local HTTP server for redirect capture
    ├── TokenExchangeService.cs # POST /oauth/token exchange
    ├── TokenExtractor.cs       # Parses code from redirect URL
    └── TokenValidator.cs       # GET /api/v4/user validation
```

> **Note:** V1 originally used the implicit grant flow (`response_type=token`) which is disabled on many GitLab instances. It was later updated to use the Authorization Code flow.

---

## V2: OAuthTokenGenV2 (Standalone Windows Executable)

A self-contained single-file Windows executable (~16 MB). No .NET SDK or runtime installation required on the target machine. Share the `.exe` file directly.

#### How to Run
```powershell
# From source
cd OAuthTokenGenV2
dotnet run

# Or use the published executable
.\OAuthTokenGenV2\publish\GitLabOAuthTokenGenerator.exe
```

#### How to Build the Standalone EXE
```powershell
cd OAuthTokenGenV2
.\publish.ps1
# Output: OAuthTokenGenV2\publish\GitLabOAuthTokenGenerator.exe
```

#### Key Improvements over V1
- **Authorization Code flow** (`response_type=code`) instead of implicit grant
- **Token refresh** using saved refresh tokens (no re-authorization needed)
- **Local token storage** in `gitlab_oauth_token.json` alongside the executable
- **Masked secret input** (asterisks while typing)
- **Three modes:** Automatic, Manual, and Refresh
- **Self-contained EXE** with trimming, ReadyToRun, and compression

#### Project Structure
```
OAuthTokenGenV2/
├── OAuthTokenGenV2.csproj        # Self-contained publish config
├── Program.cs                    # CLI with 3 modes
├── publish.ps1                   # Build script for standalone EXE
├── README.md
├── Models/
│   ├── OAuthConfig.cs
│   └── TokenResult.cs
├── Services/
│   ├── BrowserLauncher.cs
│   ├── LocalCallbackServer.cs
│   ├── TokenExchangeService.cs   # + RefreshAccessTokenAsync()
│   ├── TokenExtractor.cs
│   ├── TokenStorageService.cs    # Save/load tokens to JSON file
│   └── TokenValidator.cs
└── publish/
    └── GitLabOAuthTokenGenerator.exe  # Standalone executable (~16 MB)
```

#### Publish Settings
| Setting | Purpose |
|---|---|
| `SelfContained=true` | Bundles .NET 8.0 runtime |
| `PublishSingleFile=true` | Single `.exe` file |
| `PublishTrimmed=true` | Removes unused code |
| `PublishReadyToRun=true` | Pre-compiled for fast startup |
| `EnableCompressionInSingleFile=true` | Compressed bundle |

---

## V3: OAuthTokenGenV3 (Web Application + Docker)

A browser-based web application with a tabbed UI. Runs on Docker, .NET Kestrel, or GitLab Pages. All OAuth operations happen client-side in the browser.

#### How to Run

**Docker (Recommended):**
```powershell
cd OAuthTokenGenV3
docker build -t gitlab-oauth-token-gen:v3 .
docker run -d -p 8585:8585 --name oauth-token-gen gitlab-oauth-token-gen:v3
# Open http://localhost:8585
```

**.NET Kestrel:**
```powershell
cd OAuthTokenGenV3
dotnet run
# Open http://localhost:8585
```

**PowerShell Script:**
```powershell
cd OAuthTokenGenV3
.\serve.ps1
```

#### Key Improvements over V2
- **Web UI** with 4 tabs (Generate, Settings, Saved Tokens, VS Guide)
- **Docker support** with multi-stage build and health check
- **GitLab Pages** deployment via CI/CD
- **Export/Import** tokens as JSON files
- **Raw JSON viewer** (equivalent to V2's `gitlab_oauth_token.json`)
- **CSRF protection** with `state` parameter
- **Responsive design** for desktop and mobile
- **Zero dependencies** (no npm, no frameworks, pure HTML/CSS/JS)

#### Application Tabs
| Tab | Description |
|---|---|
| 🔐 **Generate Token** | OAuth flow, manual code entry, token refresh |
| ⚙️ **Settings** | Default values, security info, OAuth app setup guide |
| 📋 **Saved Tokens** | Token cards with status, export/import, JSON viewer |
| 📖 **VS Setup Guide** | Step-by-step instructions, troubleshooting FAQ |

#### Project Structure
```
OAuthTokenGenV3/
├── Dockerfile                # Multi-stage: SDK 8.0 (build) + ASP.NET 8.0 (runtime)
├── .dockerignore
├── OAuthTokenGenV3.csproj    # ASP.NET Core minimal web project
├── Program.cs                # Kestrel server (binds 0.0.0.0 for Docker)
├── .gitlab-ci.yml            # GitLab Pages deployment
├── serve.ps1                 # PowerShell local server script
├── README.md                 # Detailed documentation
├── DEMO_PRESENTATION.md      # 16-slide demo presentation
├── public/                   # Static files (GitLab Pages)
│   ├── index.html
│   ├── styles.css
│   └── app.js
└── wwwroot/                  # Static files (.NET Kestrel / Docker)
    ├── index.html
    ├── styles.css
    └── app.js
```

#### Docker Commands
```powershell
# Build
docker build -t gitlab-oauth-token-gen:v3 -f OAuthTokenGenV3/Dockerfile OAuthTokenGenV3/

# Run (default port 8585)
docker run -d -p 8585:8585 --name oauth-token-gen gitlab-oauth-token-gen:v3

# Run (custom port)
docker run -d -p 9090:9090 -e PORT=9090 --name oauth-token-gen gitlab-oauth-token-gen:v3

# Logs
docker logs -f oauth-token-gen

# Stop & remove
docker stop oauth-token-gen && docker rm oauth-token-gen
```

---

## V4: OAuthTokenGenExt (Visual Studio Extension)

A Visual Studio 2022/2026 extension that embeds the OAuth Token Generator directly into the IDE as a dockable tool window. Built with the VSSDK (in-process) extensibility model, WPF, and WebView2. No admin access, no local server, no Docker required.

#### How to Install

**From VSIX file (no admin required):**
```powershell
# Double-click the .vsix file, or:
"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\VSIXInstaller.exe" OAuthTokenGenExt.vsix
```

**From source:**
```powershell
cd OAuthTokenGenExt
.\build.ps1
```

> **Note:** VSIX packaging requires building from within Visual Studio. `dotnet build` compiles the DLL but the `.vsix` container needs MSBuild from the VS installation. Open `OAuthTokenGenExt.csproj` in VS 2022/2026 and build in Release mode.

#### How to Use

1. Open Visual Studio 2022 or 2026
2. Go to **Tools > GitLab OAuth Token Generator**
3. Fill in your GitLab URL, Application ID, and Secret
4. Click **Authorize with GitLab** (opens your default browser)
5. Authorize in GitLab, then copy the redirect URL from your browser's address bar
6. Paste it into the **Code / Redirect URL** field and click **Exchange Code for Token**
7. Copy the generated token into **Tools > Options > GitLab > Access Token**

#### Key Improvements over V3
- **No server required**: Runs entirely inside Visual Studio via WebView2
- **No admin access**: Per-user VSIX install to `%LOCALAPPDATA%\Microsoft\VisualStudio\`
- **IDE integration**: Dockable tool window alongside Solution Explorer, Properties, etc.
- **Persistent storage**: Settings and tokens saved to `%APPDATA%\OAuthTokenGenExt\` (survives browser cache clears)
- **Single-file distribution**: Share the `.vsix` file with team members
- **Same 4-tab UI**: Generate Token, Settings, Saved Tokens, VS Setup Guide

#### Architecture

The extension uses a **WebView2 control** inside a **WPF UserControl** hosted in a **VS Tool Window**. The HTML/CSS/JS from V3 was adapted and embedded as assembly resources. A C#/JavaScript bridge via `postMessage` handles persistence (file I/O) and native operations (clipboard, file dialogs).

```
Visual Studio Process
  └── OAuthTokenGenExtPackage (AsyncPackage)
        └── TokenGeneratorToolWindow (ToolWindowPane)
              └── TokenGeneratorControl (WPF + WebView2)
                    ├── HTML/CSS/JS (embedded resources)
                    ├── fetch() ──> GitLab Instance (/oauth/token, /api/v4/user)
                    └── postMessage ──> C# host ──> %APPDATA%\*.json
```

#### Project Structure
```
OAuthTokenGenExt/
├── OAuthTokenGenExt.csproj              # VS SDK project (net472, WebView2)
├── source.extension.vsixmanifest        # VSIX manifest (VS 2022 + 2026)
├── OAuthTokenGenExtPackage.cs           # AsyncPackage entry point
├── OAuthTokenGenExtPackage.vsct         # Command table (Tools menu item)
├── OpenTokenGeneratorCommand.cs         # Menu command handler
├── TokenGeneratorToolWindow.cs          # Tool window definition
├── TokenGeneratorControl.xaml           # WPF host for WebView2
├── TokenGeneratorControl.xaml.cs        # WebView2 init + C#/JS bridge
├── build.ps1                            # Build & install script
├── DESIGN.md                            # Architecture & build decisions
├── README.md                            # Detailed documentation
└── Resources/
    ├── TokenGenerator.html              # Main HTML UI (4 tabs)
    ├── app.css                          # Dark theme styles
    ├── app.js                           # OAuth logic + host communication
    └── icon.png                         # Extension icon
```

#### Technology Stack

| Component | Technology |
|---|---|
| Extensibility model | VSSDK (in-process), `AsyncPackage` |
| Target framework | .NET Framework 4.7.2 (`net472`) |
| UI hosting | WPF `UserControl` + WebView2 |
| UI rendering | HTML5 / CSS3 / JavaScript (embedded resources) |
| Persistence | JSON files in `%APPDATA%\OAuthTokenGenExt\` |
| C#/JS bridge | `CoreWebView2.PostWebMessageAsJson` / `WebMessageReceived` |
| NuGet packages | `Microsoft.VisualStudio.SDK`, `Microsoft.Web.WebView2`, `System.Text.Json` |

> See `OAuthTokenGenExt/DESIGN.md` for the full design document including how the extension was built, architecture diagrams, and key design decisions.

---

## GitLab OAuth Application Setup

All four versions require a GitLab OAuth application. Create one before using any version:

1. Go to **User Settings > Applications** in your GitLab instance
2. Set **Name** to `VS OAuth Token Generator`
3. Set **Redirect URI** based on your setup:

   | Setup | Redirect URI |
   |---|---|
   | V1/V2 Automatic mode | `http://localhost:8585/` |
   | V1/V2 Manual mode | `http://localhost` |
   | V3 Docker / .NET | `http://localhost:8585/` |
   | V3 GitLab Pages | `https://<namespace>.<instance>/<group>/<project>/` |
   | V4 VS Extension | `http://localhost:8585/` |

4. Check **Confidential**
5. Under Scopes, select **api**
6. Click **Save application** and note the **Application ID** and **Secret**

## Visual Studio Setup

After generating a token with any version:

1. Open **Visual Studio 2022 or 2026**
2. Go to **Tools > Options > GitLab**
3. Paste the OAuth access token (`GLOAS_...`) into the **Access Token** field
4. Enter your **GitLab URL** (e.g., `https://gitlab.example.com`)
5. Click **OK** or **Apply**

> OAuth tokens and PATs are structurally treated similarly by the GitLab API, so the OAuth token works directly in the extension's PAT text field.

## Token Expiration

OAuth access tokens expire based on the GitLab server configuration (default: 2 hours).

- **V2, V3, and V4** support **token refresh** using saved refresh tokens (no re-authorization needed)
- **Admins** can increase the expiration in `gitlab.rb`:
  ```ruby
  gitlab_rails['doorkeeper_access_token_expires_in'] = 86400  # 24 hours
  ```

## Troubleshooting

| Issue | Solution |
|---|---|
| "Authorization server does not support this response type" | Use V2 or V3 (Authorization Code flow). V1's implicit grant is disabled on many instances |
| "I don't have access to the file" in VS | Ensure files are pushed, OAuth app has `api` scope, project is cloned from GitLab |
| "Unsupported language" in Code Suggestions | Extensions > GitLab > Toggle Code Suggestions. Update extension. Restart VS |
| Token expires too quickly | Use Refresh Token (V2/V3). Or ask admin to increase `doorkeeper_access_token_expires_in` |
| Pipeline stuck (no runners) | Add runner tags to CI jobs. Check Settings > CI/CD > Runners for available tags |
| Docker container won't start | Check `docker logs oauth-token-gen`. Ensure port 8585 is not in use |
| V4 tool window blank/error | WebView2 Runtime may not be installed. Download from [Microsoft](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) |
| V4 VSIX won't install | Ensure VS 2022 (17.0+) or VS 2026 is installed. Try double-clicking the `.vsix` file |

## CI/CD Pipeline

The `.gitlab-ci.yml` at the root deploys OAuthTokenGenV3 to GitLab Pages:

```yaml
create-pages:
  tags:
    - dex-build-poc              # Runner tag (required on this instance)
  pages:
    publish: public
  script:
    - mkdir -p public
    - cp -r OAuthTokenGenV3/public/* public/
  rules:
    - if: $CI_COMMIT_REF_NAME == $CI_DEFAULT_BRANCH
```

> **Note:** Runners on GitLab Dedicated instances require specific tags. The tag `dex-build-poc` is specific to this instance. Update it to match your runner configuration.

> **V4 Note:** The VS Extension (`OAuthTokenGenExt`) is not deployed via CI/CD. It is built locally in Visual Studio and distributed as a `.vsix` file. See `OAuthTokenGenExt/DESIGN.md` for build details.

## References

- [GitLab OAuth 2.0 Documentation](https://docs.gitlab.com/ee/api/oauth2.html)
- [GitLab Duo Code Suggestions Setup](https://docs.gitlab.com/ee/user/project/repository/code_suggestions/set_up.html)
- [Visual Studio GitLab Extension](https://marketplace.visualstudio.com/items?itemName=GitLab.GitLabExtensionForVisualStudio)
- [GitLab Pages Documentation](https://docs.gitlab.com/ee/user/project/pages/)
- [GitLab CI/CD Documentation](https://docs.gitlab.com/ee/ci/)