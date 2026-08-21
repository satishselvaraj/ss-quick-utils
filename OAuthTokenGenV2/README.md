# GitLab OAuth Token Generator V2 - Standalone Windows Executable

A self-contained .NET 8.0 Windows console application that automates the generation of OAuth 2.0 access tokens for use with the Visual Studio GitLab extension. Published as a **single `.exe` file** that requires no .NET SDK or runtime installation on the target machine.

## Quick Start

1. Download `GitLabOAuthTokenGenerator.exe` from the `publish` folder
2. Double-click to run (or execute from any terminal)
3. Follow the interactive prompts

No installation required. No dependencies. Just run.

## Project Structure

```
OAuthTokenGenV2/
├── OAuthTokenGenV2.csproj            # .NET 8.0 project (self-contained, single-file publish)
├── Program.cs                        # Main entry point with interactive CLI
├── publish.ps1                       # PowerShell script to build the standalone .exe
├── README.md                         # This file
├── Models/
│   ├── OAuthConfig.cs                # OAuth configuration & authorization URL builder
│   └── TokenResult.cs                # Token extraction result model
├── Services/
│   ├── BrowserLauncher.cs            # Cross-platform default browser launcher
│   ├── LocalCallbackServer.cs        # Local HTTP server to capture OAuth redirect
│   ├── TokenExchangeService.cs       # Exchanges authorization code for access token
│   ├── TokenExtractor.cs             # Parses authorization code from redirect URL
│   ├── TokenStorageService.cs        # Saves/loads tokens locally for refresh flow
│   └── TokenValidator.cs             # Validates token via GitLab API
└── publish/
    └── GitLabOAuthTokenGenerator.exe # The standalone executable (~16 MB)
```

## Prerequisites

#### For Running the Executable
- **Windows x64** (Windows 10 or later recommended)
- No .NET SDK or runtime installation needed

#### For Building from Source
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows x64

#### GitLab Requirements
- A GitLab account (SaaS or self-managed)
- An OAuth application created in GitLab (see setup instructions below)

## Setting Up the GitLab OAuth Application

Before running the tool, create an OAuth application in your GitLab instance:

1. Navigate to your GitLab instance
2. Go to **User Settings > Applications**
   (or **Admin > Applications** for instance-wide apps on self-managed instances)
3. Fill in the application details:

   | Field | Value |
   |---|---|
   | **Name** | `Visual Studio Client` |
   | **Redirect URI** | `http://localhost:8585/` (automatic mode) or `http://localhost` (manual mode) |
   | **Confidential** | Yes (checked) |
   | **Scopes** | `api` (checked) |

4. Click **Save application**
5. Note your **Application ID** and **Application Secret** (you will need both)

> **Important:** The Redirect URI must match exactly. For automatic mode, include the trailing slash: `http://localhost:8585/`

## How to Run

#### Option 1: Double-Click the Executable
Navigate to the `publish` folder and double-click `GitLabOAuthTokenGenerator.exe`. A console window will open with the interactive prompts.

#### Option 2: Run from Terminal
```powershell
.\publish\GitLabOAuthTokenGenerator.exe
```

#### Option 3: Run from Source (Development)
```powershell
cd OAuthTokenGenV2
dotnet restore
dotnet run
```

## Two Operating Modes

#### Mode 1: Automatic (Recommended)

Starts a local HTTP server on `localhost:8585`, opens the default browser for OAuth authorization, and automatically captures the authorization code when GitLab redirects back.

```
User input → Browser opens → User authorizes → Code captured → Token exchanged → Token displayed
```

**How it works:**
1. A lightweight `HttpListener` server starts on `http://localhost:8585/`
2. The browser opens the GitLab OAuth authorization URL
3. After the user authorizes, GitLab redirects to `http://localhost:8585/?code=AUTH_CODE`
4. The server captures the `code` query parameter directly
5. The app exchanges the code for an access token via `POST /oauth/token`
6. The access token is validated and displayed

#### Mode 2: Manual

Generates the authorization URL for you to open manually. After authorizing in the browser, you copy the full redirect URL (or just the authorization code) and paste it back into the console.

```
User input → URL displayed → User opens browser → User authorizes → User pastes URL/code → Token exchanged → Token displayed
```

## What It Does (End-to-End)

1. **Prompts** for your **GitLab URL**, **OAuth Application ID**, and **Application Secret** (secret input is masked with asterisks)
2. **Builds** the OAuth authorization URL with `response_type=code&scope=api` (Authorization Code flow)
3. **Opens** the default browser and captures the authorization code from the redirect
4. **Exchanges** the authorization code for an access token via `POST /oauth/token`
5. **Validates** the token by calling `GET /api/v4/user` on your GitLab instance
6. **Displays** the token details (masked for security) with expiration information
7. **Prints** step-by-step Visual Studio setup instructions
8. **Offers** to copy the token to clipboard via PowerShell

## Injecting the Token into Visual Studio

Once the token is generated, the application displays these instructions:

1. Open **Visual Studio IDE**
2. Go to **Tools > Options > GitLab**
3. Paste the generated OAuth access token (e.g., `GLOAS_...`) into the **Access Token** field
4. Enter your **GitLab URL** (e.g., `https://gitlab.com`) and apply the changes

> **Note:** OAuth tokens and Personal Access Tokens (PATs) are structurally treated similarly by the GitLab API endpoint, so the OAuth token works directly in the extension's PAT text field.

## OAuth Flow: Authorization Code vs Implicit Grant

This application uses the **Authorization Code flow** (`response_type=code`) instead of the Implicit Grant flow (`response_type=token`). This is because:

- Many modern GitLab instances (especially self-managed) **disable the implicit grant flow** by default for security reasons
- The Authorization Code flow is the **recommended OAuth 2.0 flow** for server-side and desktop applications
- It provides a **refresh token** for token renewal without re-authorization

| Aspect | Authorization Code Flow | Implicit Grant Flow |
|---|---|---|
| `response_type` | `code` | `token` |
| Token delivery | Server-side exchange via `POST /oauth/token` | URL fragment (`#access_token=...`) |
| Requires secret | Yes | No |
| Refresh token | Yes | No |
| Security | Higher (token never exposed in browser) | Lower (token visible in URL) |
| GitLab support | Always enabled | Often disabled on self-managed |

## Building the Standalone Executable

#### Using the Publish Script
```powershell
cd OAuthTokenGenV2
.\publish.ps1
```

#### Using the dotnet CLI Directly
```powershell
cd OAuthTokenGenV2
dotnet publish OAuthTokenGenV2.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output .\publish `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=true `
    /p:PublishReadyToRun=true `
    /p:EnableCompressionInSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true
```

The output executable will be at `OAuthTokenGenV2\publish\GitLabOAuthTokenGenerator.exe`.

#### Publish Settings Explained

| Setting | Purpose |
|---|---|
| `SelfContained=true` | Bundles the entire .NET 8.0 runtime into the executable |
| `PublishSingleFile=true` | Packs all assemblies and runtime into a single `.exe` file |
| `PublishTrimmed=true` | Removes unused framework code to reduce file size |
| `PublishReadyToRun=true` | Pre-compiles (AOT) for faster application startup |
| `EnableCompressionInSingleFile=true` | Compresses the single-file bundle to reduce size |
| `IncludeNativeLibrariesForSelfExtract=true` | Includes native libraries inside the single file |
| `RuntimeIdentifier=win-x64` | Targets 64-bit Windows |

## Component Details

| Component | File | Description |
|---|---|---|
| **OAuthConfig** | `Models/OAuthConfig.cs` | Holds GitLab domain, Application ID, Application Secret, redirect URI, and scope. Builds the authorization URL (`/oauth/authorize`) and token exchange URL (`/oauth/token`). Validates all required fields. |
| **TokenResult** | `Models/TokenResult.cs` | Represents the parsed token response with access token, token type, scope, expiry, refresh token, and computed expiration timestamp. |
| **BrowserLauncher** | `Services/BrowserLauncher.cs` | Cross-platform utility that opens URLs in the default browser. Uses `Process.Start` with `UseShellExecute` on Windows, `xdg-open` on Linux, and `open` on macOS. |
| **LocalCallbackServer** | `Services/LocalCallbackServer.cs` | Lightweight `HttpListener`-based HTTP server. Listens on `localhost:8585` for the OAuth redirect, extracts the `?code=` query parameter, and serves success/error HTML pages to the browser. |
| **TokenExchangeService** | `Services/TokenExchangeService.cs` | Exchanges the authorization code for an access token by calling `POST /oauth/token`. Also handles token refresh via `grant_type=refresh_token`. |
| **TokenExtractor** | `Services/TokenExtractor.cs` | Utility to extract the authorization code from a redirect URL's `?code=` query parameter. Used in the manual flow when the user pastes the full redirect URL. |
| **TokenStorageService** | `Services/TokenStorageService.cs` | Saves and loads token data (including refresh token) to `gitlab_oauth_token.json` alongside the executable. Uses source-generated JSON for trim-safe serialization. |
| **TokenValidator** | `Services/TokenValidator.cs` | Validates the access token by calling `GET /api/v4/user` with a Bearer token header. Returns the authenticated user's name, username, and email. |

## Distributing the Executable

The `GitLabOAuthTokenGenerator.exe` file is fully self-contained. To share it:

- **File share:** Copy the `.exe` to a shared network drive
- **Email:** Attach the `.exe` directly (some email providers may block `.exe` files; use a `.zip` archive)
- **Teams/Slack:** Upload the `.exe` or a `.zip` archive
- **GitLab Releases:** Attach the `.exe` as a release asset in your GitLab project

Recipients only need **Windows x64** (Windows 10 or later). No .NET SDK, runtime, or any other software installation is required.

## Token Expiration and Refresh

#### Default Expiration

OAuth access tokens have a default expiration set **server-side by GitLab** (typically **2 hours / 7200 seconds**). The application cannot change this value on its own.

#### Increasing Token Expiration (Admin Required)

If you have admin access to a self-managed GitLab instance, you can increase the token lifetime:

1. Edit `/etc/gitlab/gitlab.rb` and add:
   ```ruby
   gitlab_rails['doorkeeper_access_token_expires_in'] = 86400  # 24 hours
   ```
2. Run `sudo gitlab-ctl reconfigure`

Common values:
| Duration | Seconds |
|---|---|
| 2 hours (default) | `7200` |
| 8 hours (workday) | `28800` |
| 24 hours | `86400` |
| 7 days | `604800` |

#### Using Refresh Tokens (No Admin Required)

The V2 application automatically saves a **refresh token** to `gitlab_oauth_token.json` alongside the executable. When your access token expires, simply run the app again and choose **Mode [3] Refresh** to get a new access token without re-authorizing in the browser.

```
  Choose a mode:
    [1] Automatic - Opens browser & captures code via local server
    [2] Manual    - Generates the URL; you paste the redirect URL back
    [3] Refresh   - Renew token using saved refresh token (EXPIRED)
```

The refresh token has a much longer lifetime than the access token and can be used repeatedly until it is revoked or the OAuth application is deleted.

## Visual Studio GitLab Extension: "I don't have access to the file" Error

If you see the error **"I don't have access to the file PaidInFullProcessListener.cs - only the class name is visible, not the actual code content"** (or similar) in the Visual Studio GitLab extension (Duo Chat / Code Suggestions), this is **not a token issue**. The token is working correctly for authentication, but the extension cannot read the file content.

#### Root Causes and Solutions

**1. Ensure the `api` scope is granted**

When creating the OAuth application, the `api` scope must be selected. This grants full read/write access to the API, including repository file contents. If you only selected `read_user` or other limited scopes, the extension cannot read source files.

To fix: Delete the OAuth application, recreate it with the **`api`** scope, and generate a new token.

**2. Open the solution/project from a Git-cloned repository**

The GitLab extension needs to associate your local files with a GitLab project. This only works when:
- The project is cloned from GitLab (not just a local folder)
- The `.git` folder exists with a valid `origin` remote pointing to your GitLab instance
- The branch you are on exists in the remote repository

To verify:
```powershell
git remote -v
# Should show: origin  https://your-gitlab-instance/namespace/project.git
```

**3. Ensure the file is committed and pushed**

The extension reads file content from the **GitLab API** (remote repository), not from your local disk. If the file:
- Is newly created but not committed
- Is committed locally but not pushed
- Is in a branch that does not exist on the remote

Then the extension cannot access it. Push your changes:
```powershell
git add .
git commit -m "Add files"
git push origin your-branch
```

**4. Check project visibility and permissions**

- The authenticated user (shown during token validation) must have at least **Reporter** access to the project
- If the project is **private**, ensure the token's user has been granted access
- For **internal** projects, the user must be logged into the GitLab instance

**5. Verify the GitLab URL in Visual Studio settings matches**

Go to **Tools > Options > GitLab** and ensure:
- The **GitLab URL** matches exactly (e.g., `https://gitlab.example.com`, not `https://gitlab.example.com/`)
- The URL uses `https://` (not `http://`)
- There are no trailing slashes or paths

**6. Extension version and IDE compatibility**

- Update the GitLab extension to the latest version via **Extensions > Manage Extensions**
- Restart Visual Studio after updating
- Some features require specific Visual Studio versions (2022 17.6+)

**7. Large files or binary files**

The extension may not be able to read:
- Files larger than the API size limit
- Binary files (images, compiled assemblies, etc.)
- Files in Git LFS that have not been fetched

## Troubleshooting

| Issue | Solution |
|---|---|
| **"The authorization server does not support this response type"** | This error occurs with the implicit grant flow. V2 uses the Authorization Code flow, which resolves this. Ensure you are running the V2 executable. |
| **"I don't have access to the file"** in VS | See the detailed section above. Usually caused by files not being pushed, missing `api` scope, or project not cloned from GitLab. |
| **Token expires too quickly** | Use Mode [3] Refresh to renew, or ask your GitLab admin to increase `doorkeeper_access_token_expires_in`. |
| **Browser does not open** | Copy the displayed URL manually from the console and open it in your browser. |
| **Port 8585 is already in use** | Close the conflicting application, or use manual mode (Mode 2) which does not require a local server. |
| **Token exchange fails with HTTP 401** | Verify that the Application Secret is correct and the OAuth application has the `api` scope. |
| **Token validation fails** | Ensure the GitLab URL is correct (include `https://`) and the OAuth application has the `api` scope enabled. |
| **Refresh token fails** | The refresh token may have expired or been revoked. Use Mode 1 or 2 to re-authorize from scratch. |
| **Clipboard copy fails** | PowerShell's `Set-Clipboard` may not be available in all environments. Manually copy the token from the console output. |
| **Timeout waiting for callback (120s)** | Complete the authorization in the browser within 120 seconds, or switch to manual mode. |
| **Windows Defender SmartScreen warning** | Since the `.exe` is not code-signed, Windows may show a warning. Click "More info" then "Run anyway". |
| **Console window closes immediately** | The V2 application always waits for a keypress before exiting. If it closes, run it from a terminal to see the error output. |

## Version History

| Version | Description |
|---|---|
| **V1** (`OAuthTokenGen`) | Initial version. Used implicit grant flow (`response_type=token`). Requires .NET SDK to run via `dotnet run`. |
| **V2** (`OAuthTokenGenV2`) | Standalone Windows executable. Authorization Code flow (`response_type=code`). Self-contained single-file publish. Token refresh support with local storage. No .NET installation required. |
