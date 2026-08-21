# GitLab OAuth Token Generator for Visual Studio

A .NET 8.0 console application that automates the generation of OAuth 2.0 access tokens for use with the Visual Studio GitLab extension. Instead of manually navigating browser redirects and copying tokens from URL fragments, this tool handles the entire flow interactively.

## Project Structure

```
OAuthTokenGen/
├── OAuthTokenGen.csproj          # .NET 8.0 console app project
├── Program.cs                    # Main entry point with interactive CLI
├── Models/
│   ├── OAuthConfig.cs            # OAuth configuration & URL builder
│   └── TokenResult.cs            # Token extraction result model
└── Services/
    ├── BrowserLauncher.cs        # Cross-platform browser opener
    ├── LocalCallbackServer.cs    # Local HTTP server to capture redirect
    ├── TokenExtractor.cs         # Parses access_token from URL fragment
    └── TokenValidator.cs         # Validates token via GitLab /api/v4/user
```

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- A GitLab account with access to create OAuth applications
- Visual Studio with the GitLab extension installed

## Setting Up the GitLab OAuth Application

Before running the tool, you need to create an OAuth application in GitLab:

1. Navigate to your GitLab instance and go to **User Settings > Applications**
   (or **Admin > Applications** for instance-wide apps on self-managed instances).
2. Set the **Name** to `Visual Studio Client`.
3. Set the **Redirect URI** to `http://localhost:8585/` (for automatic mode) or `http://localhost` (for manual mode).
4. Under **Scopes**, select **api**.
5. Click **Save application** and note your **Application ID** and **Secret**.

## How to Run

```powershell
cd OAuthTokenGen
dotnet restore
dotnet run
```

## Two Operating Modes

### Mode 1: Automatic (Recommended)

Starts a local HTTP server on `localhost:8585`, opens the browser for OAuth authorization, and automatically captures the token when GitLab redirects back.

The HTML page served by the local server uses JavaScript to read the URL fragment (since `#access_token=...` is never sent to the server by the browser) and posts it back to the local server for extraction.

**Flow:**
```
Console prompts → Browser opens → User authorizes → Redirect captured → Token displayed
```

### Mode 2: Manual

Generates the authorization URL for you to open in a browser. After authorizing, you copy the full redirect URL from the browser address bar and paste it back into the console.

**Flow:**
```
Console prompts → URL displayed → User opens browser → User authorizes → User pastes redirect URL → Token displayed
```

## What It Does (End-to-End)

1. **Prompts** for your **GitLab URL** and **OAuth Application ID** (from GitLab > User Settings > Applications).
2. **Builds** the OAuth authorize URL with `response_type=token&scope=api`.
3. **Opens** the browser and captures the `GLOAS_...` token from the redirect.
4. **Validates** the token by calling `GET /api/v4/user` on your GitLab instance.
5. **Displays** the token details and prints **Visual Studio setup instructions**.
6. **Offers** to copy the token to clipboard via PowerShell.

## Injecting the Token into Visual Studio

Once the token is generated:

1. Open **Visual Studio IDE**.
2. Go to **Tools > Options > GitLab**.
3. Paste the generated OAuth access token (`GLOAS_...`) into the **Access Token** field.
4. Enter your **GitLab URL** (e.g., `https://gitlab.com`) and apply the changes.

> **Note:** OAuth tokens and PATs are structurally treated similarly by the GitLab API, so the OAuth token works directly in the extension's PAT text field.

## Component Details

| Component | Description |
|---|---|
| **OAuthConfig** | Holds the GitLab domain, Application ID, redirect URI, and scope. Builds the authorization URL and validates required fields. |
| **TokenResult** | Represents the parsed token with access token, token type, scope, expiry, and computed expiration timestamp. |
| **BrowserLauncher** | Cross-platform utility that opens URLs in the default browser on Windows, Linux, and macOS. |
| **LocalCallbackServer** | Lightweight `HttpListener`-based server that serves an HTML/JS page to capture the URL fragment and receive it via a POST callback. |
| **TokenExtractor** | Parses the redirect URL fragment (`#access_token=...&token_type=Bearer&...`) into a structured `TokenResult`. |
| **TokenValidator** | Calls `GET /api/v4/user` with the Bearer token to confirm it is valid and displays the authenticated user's identity. |

## Troubleshooting

| Issue | Solution |
|---|---|
| Browser does not open | Copy the displayed URL manually and open it in your browser. |
| Port 8585 is in use | Close the conflicting application or modify the port in `Program.cs`. |
| Token validation fails | Ensure the OAuth application has the `api` scope and the GitLab URL is correct. |
| Clipboard copy fails | Manually copy the token from the console output. |
| Timeout waiting for callback | Authorize within 120 seconds, or switch to manual mode. |
