# GitLab OAuth Token Generator V3

A browser-based web application that generates OAuth 2.0 access tokens for the Visual Studio GitLab extension. All OAuth operations run entirely client-side. Deployable via **Docker**, **.NET Kestrel**, **GitLab Pages**, or as a simple static file.

## Quick Start

Choose any of the four ways to run the application:

#### Option 1: Docker Desktop (Recommended)
```powershell
cd OAuthTokenGenV3
docker build -t gitlab-oauth-token-gen:v3 .
docker run -d -p 8585:8585 --name oauth-token-gen gitlab-oauth-token-gen:v3
# Open http://localhost:8585
```

#### Option 2: .NET Kestrel
```powershell
cd OAuthTokenGenV3
dotnet run
# Open http://localhost:8585
```

#### Option 3: GitLab Pages
Push to GitLab. The CI pipeline deploys automatically to GitLab Pages on the default branch.

#### Option 4: PowerShell Serve Script
```powershell
cd OAuthTokenGenV3
.\serve.ps1              # Default port 8585
.\serve.ps1 -Port 9090   # Custom port
```

## Project Structure

```
OAuthTokenGenV3/
├── Dockerfile                # Multi-stage Docker build (SDK 8.0 + ASP.NET 8.0 runtime)
├── .dockerignore             # Excludes build artifacts from Docker context
├── OAuthTokenGenV3.csproj    # ASP.NET Core minimal web project
├── Program.cs                # Kestrel server serving static files
├── .gitlab-ci.yml            # GitLab Pages deployment pipeline
├── serve.ps1                 # PowerShell local server script
├── README.md                 # This file
├── public/                   # Static files for GitLab Pages deployment
│   ├── index.html            # Main HTML with 4 tabbed sections
│   ├── styles.css            # Complete responsive stylesheet
│   └── app.js                # Application logic (OAuth, storage, UI)
└── wwwroot/                  # Static files for .NET Kestrel / Docker
    ├── index.html            # Same as public/index.html
    ├── styles.css            # Same as public/styles.css
    └── app.js                # Same as public/app.js
```

> **Note:** `public/` is used by GitLab Pages. `wwwroot/` is used by ASP.NET Core / Docker. Both contain identical files.

## Features

| Feature | Description |
|---|---|
| **Generate Token** | Full OAuth Authorization Code flow with automatic redirect capture |
| **Manual Code Entry** | Paste a code or redirect URL if the automatic redirect fails |
| **Refresh Token** | Renew expired tokens without re-authorizing in the browser |
| **Settings** | Save GitLab URL, Application ID, and Secret for reuse |
| **Token Viewer** | View all saved tokens with valid/expired status badges |
| **JSON Viewer** | Raw JSON view of all stored data (equivalent to V2's `gitlab_oauth_token.json`) |
| **Export/Import** | Export tokens as a JSON file, import from a previously exported file |
| **VS Setup Guide** | Step-by-step instructions with one-click copy buttons |
| **Troubleshooting** | FAQ for common issues (file access, unsupported language, expiration) |
| **CSRF Protection** | Uses a `state` parameter to prevent cross-site request forgery |
| **Clipboard Copy** | One-click copy for tokens, URLs, and settings |
| **Docker Support** | Multi-stage Dockerfile with health check |

## Application Tabs

| Tab | Icon | Description |
|---|---|---|
| **Generate Token** | 🔐 | Enter OAuth credentials, authorize with GitLab, exchange code for token, refresh existing tokens |
| **Settings** | ⚙️ | Configure defaults, view security info, see GitLab OAuth app setup instructions with copyable redirect URI |
| **Saved Tokens** | 📋 | View/manage all tokens with status, export/import JSON, raw JSON viewer, delete individual tokens |
| **VS Setup Guide** | 📖 | Visual Studio configuration steps with copy buttons, troubleshooting FAQ |

## How It Works

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  User enters │     │  GitLab shows    │     │  GitLab redirects│
│  credentials │────>│  authorization   │────>│  back with       │
│  & clicks    │     │  page            │     │  ?code=AUTH_CODE │
│  Authorize   │     │                  │     │                  │
└─────────────┘     └──────────────────┘     └────────┬────────┘
                                                       │
                    ┌──────────────────┐               │
                    │  Token validated  │     ┌────────▼────────┐
                    │  via GET          │<────│  JS calls POST   │
                    │  /api/v4/user     │     │  /oauth/token    │
                    │                  │     │  to exchange code │
                    └────────┬─────────┘     └─────────────────┘
                             │
                    ┌────────▼─────────┐
                    │  Token saved to   │
                    │  localStorage     │
                    │  & displayed      │
                    └──────────────────┘
```

1. User enters GitLab URL, Application ID, and Application Secret
2. Browser redirects to GitLab's `/oauth/authorize` endpoint
3. User authorizes the application in GitLab
4. GitLab redirects back to this page with `?code=AUTH_CODE`
5. JavaScript calls `POST /oauth/token` directly from the browser to exchange the code
6. Token is validated via `GET /api/v4/user`
7. Token and refresh token are saved to `localStorage`

All API calls go directly from the browser to the GitLab instance. No data passes through any intermediary server.

## Setting Up the GitLab OAuth Application

Before using the tool, create an OAuth application in your GitLab instance:

1. Navigate to **User Settings > Applications** (or **Admin > Applications** for instance-wide)
2. Fill in the application details:

   | Field | Value |
   |---|---|
   | **Name** | `VS OAuth Token Generator` |
   | **Redirect URI** | See table below |
   | **Confidential** | Yes (checked) |
   | **Scopes** | `api` (checked) |

3. Click **Save application** and note the **Application ID** and **Secret**

#### Redirect URI by Deployment Method

| Method | Redirect URI |
|---|---|
| Docker / .NET Kestrel | `http://localhost:8585/` |
| GitLab Pages | `https://<namespace>.gitlab.io/<project>/` |
| GitLab Dedicated Pages | `https://<namespace>.<instance>/<group>/<project>/` |
| Custom port | `http://localhost:<port>/` |

## Docker

#### Build the Image
```powershell
cd OAuthTokenGenV3
docker build -t gitlab-oauth-token-gen:v3 .
```

#### Run the Container
```powershell
docker run -d -p 8585:8585 --name oauth-token-gen gitlab-oauth-token-gen:v3
```

#### Open in Browser
```powershell
Start-Process http://localhost:8585/
```

#### Custom Port
```powershell
docker run -d -p 9090:9090 -e PORT=9090 --name oauth-token-gen gitlab-oauth-token-gen:v3
```

#### View Logs
```powershell
docker logs oauth-token-gen
docker logs -f oauth-token-gen    # Follow logs
```

#### Stop and Remove
```powershell
docker stop oauth-token-gen
docker rm oauth-token-gen
```

#### Docker Image Details

The Dockerfile uses a **multi-stage build** for a minimal production image:

| Stage | Base Image | Purpose |
|---|---|---|
| **build** | `mcr.microsoft.com/dotnet/sdk:8.0` | Restores dependencies, compiles, and publishes the app |
| **runtime** | `mcr.microsoft.com/dotnet/aspnet:8.0` | Minimal runtime image (~220 MB) with only the published output |

Additional features:
- **Health check** every 30 seconds via `curl http://localhost:8585/`
- **PORT** environment variable to customize the listening port
- Binds to `0.0.0.0` for proper Docker networking

## .NET Kestrel (Local Development)

```powershell
cd OAuthTokenGenV3
dotnet restore
dotnet run
# App starts on http://localhost:8585/
```

The ASP.NET Core minimal API serves static files from the `wwwroot/` folder using Kestrel.

## GitLab Pages Deployment

The `.gitlab-ci.yml` deploys the `public/` folder to GitLab Pages.

#### For GitLab Dedicated / Self-Managed Instances

Runners on GitLab Dedicated instances often require specific **tags**. The root `.gitlab-ci.yml` in this project uses:

```yaml
create-pages:
  tags:
    - dex-build-poc          # Required runner tag for this instance
  pages:
    publish: public
  script:
    - mkdir -p public
    - cp -r OAuthTokenGenV3/public/* public/
  rules:
    - if: $CI_COMMIT_REF_NAME == $CI_DEFAULT_BRANCH
```

> **Important:** If your runners have `run_untagged=false`, you must add the correct `tags:` to every CI job. Check available tags at **Settings > CI/CD > Runners**.

## Data Storage

All data is stored in the browser's `localStorage`. Nothing is sent to any server other than your GitLab instance.

| Key | Contents |
|---|---|
| `gitlab_oauth_settings` | GitLab URL, Application ID, Secret, Scope |
| `gitlab_oauth_tokens` | Array of all generated tokens with metadata |
| `gitlab_oauth_pending_state` | Temporary state for CSRF protection during OAuth flow |

No cookies, no server-side storage, no external analytics, no telemetry.

## Token Expiration and Refresh

OAuth access tokens have a default expiration set server-side by GitLab (typically **2 hours / 7200 seconds**).

#### Refreshing Tokens

When a token expires, the app shows a **Refresh** section on the Generate Token tab. Click **Refresh Access Token** to get a new token without re-authorizing in the browser. The refresh token is saved automatically.

#### Increasing Token Expiration (Admin Required)

On self-managed GitLab instances, admins can increase the token lifetime:

```ruby
# /etc/gitlab/gitlab.rb
gitlab_rails['doorkeeper_access_token_expires_in'] = 86400  # 24 hours
```

Then run `sudo gitlab-ctl reconfigure`.

## CORS Considerations

The token exchange (`POST /oauth/token`) and validation (`GET /api/v4/user`) calls are made directly from the browser. For this to work:

- **GitLab.com:** CORS headers are configured to allow browser requests
- **GitLab Dedicated / Self-Managed:** CORS is typically enabled. If blocked, use the **Manual Code Entry** mode as a fallback
- If CORS blocks the token exchange, the manual mode lets you complete the flow by pasting the redirect URL

## Visual Studio Setup

After generating a token:

1. Open **Visual Studio 2022**
2. Go to **Tools > Options > GitLab**
3. Paste the OAuth access token (`GLOAS_...`) into the **Access Token** field
4. Enter your **GitLab URL** and click **Apply**

> OAuth tokens and PATs are structurally treated similarly by the GitLab API, so the OAuth token works directly in the extension's PAT text field.

## Troubleshooting

| Issue | Solution |
|---|---|
| **"I don't have access to the file"** in VS | Ensure files are committed and pushed, OAuth app has `api` scope, project is cloned from GitLab, user has Reporter+ access |
| **"Unsupported language"** in Code Suggestions | Go to Extensions > GitLab > Toggle Code Suggestions. Update the extension. Restart VS. |
| **Token expires too quickly** | Use the Refresh Token feature. Admins can increase `doorkeeper_access_token_expires_in` in `gitlab.rb` |
| **CORS error on token exchange** | Use Manual Code Entry mode. Paste the full redirect URL or just the authorization code |
| **Docker container won't start** | Check `docker logs oauth-token-gen`. Ensure port 8585 is not in use |
| **GitLab Pages pipeline stuck** | Runners require tags. Add `tags: [your-runner-tag]` to the CI job. Check Settings > CI/CD > Runners |
| **Refresh token fails** | The refresh token may have expired or been revoked. Re-authorize using the Generate tab |
| **Browser shows blank page** | Check browser console (F12) for errors. Ensure `wwwroot/` or `public/` contains all 3 files |

## Component Details

| Component | File | Description |
|---|---|---|
| **HTML** | `public/index.html` | Main page with 4 tabbed sections, forms, token display, step indicators, FAQ accordions |
| **CSS** | `public/styles.css` | Responsive stylesheet with GitLab-inspired purple theme, card layout, toast notifications |
| **JavaScript** | `public/app.js` | OAuth flow (authorize, exchange, refresh), localStorage management, token list rendering, JSON viewer, clipboard, CSRF protection |
| **Kestrel Server** | `Program.cs` | ASP.NET Core minimal API serving static files. Binds to `0.0.0.0:{PORT}` for Docker compatibility |
| **Docker** | `Dockerfile` | Multi-stage build: SDK 8.0 (build) + ASP.NET 8.0 (runtime). Includes health check |
| **CI/CD** | `.gitlab-ci.yml` | GitLab Pages deployment. Copies `public/` to Pages artifact |
| **Serve Script** | `serve.ps1` | PowerShell script that auto-detects Python, Node.js, or .NET and starts a local server |

## Security

- **Client-side only:** All OAuth operations happen in the browser. No backend server processes or stores tokens
- **Direct API calls:** Token exchange and validation requests go directly from the browser to your GitLab instance
- **CSRF protection:** A random `state` parameter is generated and verified on callback
- **Secret storage:** The Application Secret is stored in `localStorage` and used only for direct requests to your GitLab instance
- **No telemetry:** No analytics, tracking, or external service calls

## Version History

| Version | Type | Hosting | Description |
|---|---|---|---|
| **V1** | .NET Console App | `dotnet run` | Implicit grant flow (`response_type=token`). Requires .NET SDK |
| **V2** | .NET Standalone EXE | Double-click `.exe` | Authorization Code flow. Self-contained Windows executable. Token refresh. No .NET required |
| **V3** | Static Web App | Docker, .NET, GitLab Pages | Browser-based. Authorization Code flow. 4-tab UI. Docker support. GitLab Pages deployment. Export/import JSON |
