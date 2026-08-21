---
marp: true
theme: default
paginate: true
backgroundColor: #ffffff
---

# GitLab OAuth Token Generator
## Demo Presentation

---

## Slide 1: The Problem

### Why do we need this tool?

**Setting up GitLab Duo in Visual Studio requires an OAuth token.**

The manual process is painful:

1. Navigate to GitLab > User Settings > Applications
2. Create an OAuth app with the correct scopes and redirect URI
3. Construct a long authorization URL manually
4. Open it in a browser, authorize, and get redirected
5. Extract the token from the URL fragment or query string
6. Paste it into Visual Studio > Tools > Options > GitLab

**Common issues developers face:**

- "The authorization server does not support this response type"
- Tokens expire every 2 hours with no easy way to refresh
- "I don't have access to the file" errors in VS after setup
- No visibility into token status (valid vs expired)

> **This tool automates the entire flow in under 30 seconds.**

---

## Slide 2: Solution Overview

### Three Versions, One Goal

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   V1: Console App          V2: Standalone EXE     V3: Web App  │
│   ┌──────────────┐        ┌──────────────┐       ┌───────────┐ │
│   │  dotnet run  │   -->  │  Double-click │  -->  │  Browser  │ │
│   │  .NET SDK    │        │  No install   │       │  Docker   │ │
│   │  required    │        │  Windows only │       │  Pages    │ │
│   └──────────────┘        └──────────────┘       └───────────┘ │
│                                                                 │
│   Implicit Grant     Auth Code Flow        Auth Code Flow      │
│   (deprecated)       + Refresh Token       + Refresh Token     │
│                      + Token Storage       + Web UI             │
│                                            + Export/Import     │
│                                            + Docker            │
│                                            + GitLab Pages      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Slide 3: V3 Architecture

### How It Works (Authorization Code Flow)

```
  Developer                    This App                     GitLab
  ────────                    ────────                     ──────
      │                           │                           │
      │  1. Enter credentials     │                           │
      │  ─────────────────────>   │                           │
      │                           │                           │
      │  2. Redirect to GitLab    │                           │
      │  <─────────────────────   │                           │
      │                           │                           │
      │  3. Authorize             │                           │
      │  ──────────────────────────────────────────────────>  │
      │                           │                           │
      │  4. Redirect with ?code=  │                           │
      │  <──────────────────────────────────────────────────  │
      │                           │                           │
      │                           │  5. POST /oauth/token     │
      │                           │  ──────────────────────>  │
      │                           │                           │
      │                           │  6. Access + Refresh Token│
      │                           │  <──────────────────────  │
      │                           │                           │
      │                           │  7. GET /api/v4/user      │
      │                           │  ──────────────────────>  │
      │                           │                           │
      │  8. Token displayed       │  9. User info returned    │
      │  <─────────────────────   │  <──────────────────────  │
      │                           │                           │
```

**Key point:** All API calls go directly from the browser to GitLab. No intermediary server.

---

## Slide 4: The Web Interface

### Four Tabs

```
┌──────────────┬──────────┬───────────────┬────────────────┐
│ 🔐 Generate  │ ⚙️ Settings│ 📋 Saved Tokens│ 📖 VS Guide   │
│   Token      │          │               │                │
├──────────────┴──────────┴───────────────┴────────────────┤
│                                                          │
│  Tab 1: Generate Token                                   │
│  ┌────────────────────────────────────────────────────┐  │
│  │  GitLab URL:        [https://gitlab.example.com ]  │  │
│  │  Application ID:    [abc123def456              ]   │  │
│  │  Application Secret:[••••••••••••••••          ]   │  │
│  │  Redirect URI:      [http://localhost:8585/    ]   │  │
│  │                                                    │  │
│  │  [ 🚀 Authorize with GitLab ]  [ 💾 Save Settings]│  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  Tab 2: Settings                                         │
│  - Default values for reuse                              │
│  - Security info (all data in localStorage)              │
│  - GitLab OAuth app setup instructions                   │
│                                                          │
│  Tab 3: Saved Tokens                                     │
│  - Token cards with Valid/Expired badges                  │
│  - Export/Import JSON                                     │
│  - Raw JSON viewer                                        │
│                                                          │
│  Tab 4: VS Setup Guide                                   │
│  - Step-by-step with copy buttons                        │
│  - Troubleshooting FAQ                                   │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

## Slide 5: Token Result Display

### After Successful Authorization

```
┌─────────────────────────────────────────────────────────┐
│  ✅ Token Generated Successfully                        │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Access Token    GLOAS_abc123def456ghi7...  [📋 Copy]   │
│  Token Type      Bearer                                 │
│  Scope           api                                    │
│  Expires In      7200 seconds (2h 0m)                   │
│  Expires At      2026-08-21 20:30:00 UTC                │
│  Refresh Token   Available (saved)                      │
│  Authenticated   John Doe (@johndoe) - john@example.com │
│                                                         │
└─────────────────────────────────────────────────────────┘

  Tip: When this token expires, run the app again and choose
       Refresh to renew without re-authorizing.
```

---

## Slide 6: Four Ways to Run

### Deployment Options

| Method | Command | Best For |
|---|---|---|
| **🐳 Docker** | `docker run -d -p 8585:8585 gitlab-oauth-token-gen:v3` | Production, sharing with team |
| **🟣 .NET Kestrel** | `dotnet run` | Local development |
| **🦊 GitLab Pages** | Push to GitLab (auto-deploys) | Organization-wide access |
| **📜 PowerShell** | `.\serve.ps1` | Quick local testing |

```
┌──────────────────────────────────────────────────────────┐
│                    Docker Desktop                         │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Container: oauth-token-gen                        │  │
│  │  Image:     gitlab-oauth-token-gen:v3              │  │
│  │  Status:    Running (healthy)                      │  │
│  │  Port:      0.0.0.0:8585 -> 8585/tcp              │  │
│  │  URL:       http://localhost:8585/                 │  │
│  └────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

---

## Slide 7: Docker Build Details

### Multi-Stage Dockerfile

```dockerfile
# Stage 1: Build (SDK image ~900 MB, discarded after build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
COPY OAuthTokenGenV3.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime (ASP.NET image ~220 MB, final image)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
COPY --from=build /app/publish .
EXPOSE 8585
HEALTHCHECK CMD curl -f http://localhost:8585/ || exit 1
ENTRYPOINT ["dotnet", "OAuthTokenGenV3.dll"]
```

**Result:** A minimal ~220 MB production image with health monitoring.

---

## Slide 8: Key Features Deep Dive

### Token Refresh (No Re-Authorization)

```
  Token Expired?
       │
       ▼
  ┌─────────────────────────────────┐
  │  Mode [3] Refresh               │
  │  ┌───────────────────────────┐  │
  │  │  GitLab: gitlab.com      │  │
  │  │  Created: 2026-08-21     │  │
  │  │  Status: EXPIRED         │  │
  │  │                          │  │
  │  │  [🔄 Refresh Token]     │  │
  │  └───────────────────────────┘  │
  └─────────────────────────────────┘
       │
       ▼
  New token in 2 seconds!
  (No browser, no re-login)
```

### Export / Import JSON

```
  Export: Download gitlab_oauth_tokens.json
  Import: Upload a previously exported file
  
  Use case: Share tokens across machines
            or back up before clearing browser data
```

---

## Slide 9: Security Model

### Where Does Data Live?

```
┌─────────────────────────────────────────────────────────┐
│                     YOUR BROWSER                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │  localStorage                                     │  │
│  │  ├── gitlab_oauth_settings (URL, ID, Secret)      │  │
│  │  ├── gitlab_oauth_tokens   (tokens array)         │  │
│  │  └── gitlab_oauth_pending_state (CSRF temp)       │  │
│  └───────────────────────────────────────────────────┘  │
│                         │                                │
│                         │ Direct API calls               │
│                         ▼                                │
│              ┌─────────────────────┐                     │
│              │  YOUR GITLAB        │                     │
│              │  INSTANCE           │                     │
│              │  POST /oauth/token  │                     │
│              │  GET  /api/v4/user  │                     │
│              └─────────────────────┘                     │
│                                                          │
│  ✅ No intermediary server                               │
│  ✅ No cookies                                           │
│  ✅ No analytics or telemetry                            │
│  ✅ No external service calls                            │
│  ✅ CSRF protection via state parameter                  │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

---

## Slide 10: Visual Studio Integration

### After Token Generation

```
  Visual Studio 2022
  ┌──────────────────────────────────────────────────┐
  │  Tools > Options > GitLab                        │
  │  ┌──────────────────────────────────────────┐   │
  │  │                                          │   │
  │  │  Access Token:                           │   │
  │  │  ┌──────────────────────────────────┐   │   │
  │  │  │ GLOAS_abc123def456ghi789jkl...   │   │   │
  │  │  └──────────────────────────────────┘   │   │
  │  │                                          │   │
  │  │  GitLab URL:                             │   │
  │  │  ┌──────────────────────────────────┐   │   │
  │  │  │ https://gitlab.example.com       │   │   │
  │  │  └──────────────────────────────────┘   │   │
  │  │                                          │   │
  │  │  [ OK ]  [ Cancel ]  [ Apply ]          │   │
  │  └──────────────────────────────────────────┘   │
  └──────────────────────────────────────────────────┘

  ✅ Duo Chat works
  ✅ Code Suggestions works
  ✅ Code Review works
```

---

## Slide 11: Common Issues Solved

### Problems This Tool Addresses

| Problem | Root Cause | How This Tool Helps |
|---|---|---|
| "Authorization server does not support this response type" | GitLab disabled implicit grant flow | Uses Authorization Code flow (`response_type=code`) |
| Token expires every 2 hours | Default GitLab setting | One-click refresh using saved refresh token |
| "I don't have access to the file" | Files not pushed, wrong scope, or URL mismatch | VS Setup Guide tab with checklist |
| "Unsupported language" in Code Suggestions | Extension not toggled or outdated | Troubleshooting FAQ with steps |
| Manual URL construction is error-prone | Complex OAuth URL parameters | Auto-generates the correct URL |
| No way to check if token is still valid | No built-in validation | Validates via `GET /api/v4/user` and shows status |
| Sharing setup across team | Each developer does it manually | Docker image or GitLab Pages for team-wide access |

---

## Slide 12: Demo Script

### Live Demo Steps (5 minutes)

**Step 1: Start the app (30 sec)**
```powershell
docker run -d -p 8585:8585 --name demo gitlab-oauth-token-gen:v3
Start-Process http://localhost:8585/
```

**Step 2: Configure (30 sec)**
- Enter GitLab URL
- Enter Application ID and Secret
- Note the auto-populated Redirect URI

**Step 3: Generate Token (60 sec)**
- Click "Authorize with GitLab"
- Authorize in GitLab
- Watch the 3-step progress indicator
- See the token result with user info

**Step 4: Show Saved Tokens tab (30 sec)**
- View token card with Valid/Expired status
- Show the raw JSON viewer
- Export as JSON file

**Step 5: Show VS Setup Guide tab (30 sec)**
- Copy token with one click
- Copy GitLab URL with one click
- Show troubleshooting FAQ

**Step 6: Demonstrate Refresh (30 sec)**
- Go back to Generate Token tab
- Show the Refresh section
- Click Refresh to get a new token instantly

**Step 7: Cleanup (30 sec)**
```powershell
docker stop demo && docker rm demo
```

---

## Slide 13: Version Comparison

### Evolution of the Tool

```
  V1 (Console)              V2 (Standalone EXE)         V3 (Web App)
  ─────────────             ───────────────────         ─────────────
  
  ✅ Basic OAuth flow       ✅ Auth Code flow           ✅ Auth Code flow
  ❌ Implicit grant only    ✅ Token refresh             ✅ Token refresh
  ❌ No refresh             ✅ Token validation          ✅ Token validation
  ❌ Requires .NET SDK      ✅ No .NET required          ✅ No install required
  ❌ CLI only               ✅ CLI with colors           ✅ Web UI with tabs
  ❌ No token storage       ✅ JSON file storage         ✅ localStorage
  ❌ No export              ❌ No export                 ✅ Export/Import JSON
  ❌ Single platform        ✅ Windows only              ✅ Any browser
  ❌ No Docker              ❌ No Docker                 ✅ Docker support
  ❌ No Pages               ❌ No Pages                  ✅ GitLab Pages
```

---

## Slide 14: Technology Stack

### What Powers V3

| Layer | Technology | Purpose |
|---|---|---|
| **Frontend** | HTML5, CSS3, Vanilla JS | Zero-dependency UI with tabs, forms, toasts |
| **Server** | ASP.NET Core 8.0 Minimal API | Kestrel serves static files |
| **Container** | Docker (multi-stage build) | Portable deployment |
| **CI/CD** | GitLab CI/CD + Pages | Automated deployment |
| **Auth** | OAuth 2.0 Authorization Code | Secure token generation |
| **Storage** | Browser localStorage | Client-side persistence |
| **Security** | CSRF state parameter | Prevents cross-site attacks |

**Zero external dependencies.** No npm, no frameworks, no build tools.

---

## Slide 15: Getting Started

### For Your Team

**Option A: Share the Docker image**
```powershell
# Build once
docker build -t gitlab-oauth-token-gen:v3 -f OAuthTokenGenV3/Dockerfile OAuthTokenGenV3/

# Push to your container registry
docker tag gitlab-oauth-token-gen:v3 registry.example.com/tools/oauth-token-gen:v3
docker push registry.example.com/tools/oauth-token-gen:v3

# Team members run
docker pull registry.example.com/tools/oauth-token-gen:v3
docker run -d -p 8585:8585 registry.example.com/tools/oauth-token-gen:v3
```

**Option B: Deploy to GitLab Pages**
- Push to GitLab, pipeline deploys automatically
- Team accesses via the Pages URL
- No local installation needed

**Option C: Share the V2 standalone EXE**
- Single `GitLabOAuthTokenGenerator.exe` file (~16 MB)
- Double-click to run, no installation
- Windows only

---

## Slide 16: Summary

### What We Built

| What | Details |
|---|---|
| **Problem** | Manual OAuth token setup for Visual Studio GitLab extension is complex and error-prone |
| **Solution** | Automated token generator with web UI, Docker support, and GitLab Pages deployment |
| **Time saved** | From ~10 minutes manual setup to ~30 seconds |
| **Versions** | V1 (Console) > V2 (Standalone EXE) > V3 (Web App + Docker) |
| **Security** | Client-side only, no intermediary server, CSRF protection |
| **Deployment** | Docker, .NET, GitLab Pages, or static file |

### Links

- **Project:** `https://trgl.gitlab-dedicated.com/lab/g-app-gitlab-saas-lab/pso-duo-sandbox/ss-quick-utils`
- **Docker:** `docker run -d -p 8585:8585 gitlab-oauth-token-gen:v3`
- **Local:** `cd OAuthTokenGenV3 && dotnet run`

---

*Built with GitLab Duo Chat assistance*
