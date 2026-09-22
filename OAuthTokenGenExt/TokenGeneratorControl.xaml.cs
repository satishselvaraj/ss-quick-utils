using Microsoft.VisualStudio.Shell;
using OAuthTokenGenExt.Services;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VsBrushes = Microsoft.VisualStudio.Shell.VsBrushes;

namespace OAuthTokenGenExt
{
    /// <summary>
    /// Native WPF UserControl for the OAuth Token Generator.
    /// - Reads credentials from ~/.gitconfig on load
    /// - Generates tokens via OAuth Authorization Code flow
    /// - Auto-refreshes every 2 hours (5 min before expiry)
    /// - Pushes tokens directly into GitLab Duo extension settings
    /// </summary>
    public partial class TokenGeneratorControl : UserControl
    {
        private readonly OAuthTokenService _tokenService = new();
        private DispatcherTimer? _refreshTimer;
        private DispatcherTimer? _countdownTimer;
        private string _currentGitLabUrl = "";
        private string _currentClientId = "";
        private string _currentClientSecret = "";
        private string? _lastUsedRedirectUri;
        private const string DefaultScope = "api read_user ai_features";

        public TokenGeneratorControl()
        {
            InitializeComponent();
        }

        // ---- Initialization ----

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AppendLog("Initializing...", LogLevel.Info);

            // Read credentials from .gitconfig
            try
            {
                var (url, clientId, clientSecret) = GitConfigService.ReadCredentials();

                if (!string.IsNullOrEmpty(url))
                {
                    GitLabUrlInput.Text = url;
                    AppendLog($"GitLab URL loaded from .gitconfig: {url}", LogLevel.Success);
                }

                if (!string.IsNullOrEmpty(clientId))
                {
                    ClientIdInput.Text = clientId;
                    AppendLog("Application ID loaded from .gitconfig", LogLevel.Success);
                }

                if (!string.IsNullOrEmpty(clientSecret))
                {
                    ClientSecretInput.Password = clientSecret;
                    AppendLog("Application Secret loaded from .gitconfig", LogLevel.Success);
                }

                if (string.IsNullOrEmpty(url) && string.IsNullOrEmpty(clientId))
                    AppendLog("No credentials found in .gitconfig. Enter them below.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                AppendLog($"Could not read .gitconfig: {ex.Message}", LogLevel.Error);
            }

            // Start countdown timer for UI updates (every 30s)
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _countdownTimer.Tick += (_, __) => UpdateStatusDisplay();
            _countdownTimer.Start();

            AppendLog("Ready. Enter credentials and click Authorize.", LogLevel.Info);
        }

        // ---- OAuth Flow (one-click with loopback listener) ----

        private async void OnAuthorizeClick(object sender, RoutedEventArgs e)
        {
            var gitlabUrl = GitLabUrlInput.Text.Trim();
            var clientId = ClientIdInput.Text.Trim();
            var clientSecret = ClientSecretInput.Password.Trim();

            if (string.IsNullOrEmpty(gitlabUrl)) { AppendLog("GitLab URL is required.", LogLevel.Error); return; }
            if (string.IsNullOrEmpty(clientId)) { AppendLog("Application ID is required.", LogLevel.Error); return; }
            if (string.IsNullOrEmpty(clientSecret)) { AppendLog("Application Secret is required.", LogLevel.Error); return; }

            SaveCredentialsToGitConfig(gitlabUrl, clientId, clientSecret);

            _currentGitLabUrl = gitlabUrl;
            _currentClientId = clientId;
            _currentClientSecret = clientSecret;

            SetButtonsEnabled(false);

            // Generate CSRF state
            var stateBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(stateBytes);
            var state = BitConverter.ToString(stateBytes).Replace("-", "").ToLowerInvariant();

            // Start loopback listener on a random port
            using var listener = new LoopbackHttpListener();
            var redirectUri = listener.RedirectUri;

            AppendLog($"Started local listener on {redirectUri}", LogLevel.Info);
            ListeningBanner.Visibility = Visibility.Visible;
            ListeningText.Text = $"Listening on {redirectUri} ... Waiting for authorization.";

            var authorizeUrl = OAuthTokenService.BuildAuthorizeUrl(gitlabUrl, clientId, redirectUri, DefaultScope, state);

            AppendLog("Opening GitLab authorization page in browser...", LogLevel.Info);
            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

            try
            {
                // Wait for the callback (up to 5 minutes)
                var code = await listener.WaitForCallbackAsync(state, TimeSpan.FromMinutes(5));

                ListeningBanner.Visibility = Visibility.Collapsed;
                AppendLog($"Authorization code received: {code.Substring(0, Math.Min(10, code.Length))}...", LogLevel.Success);

                // Exchange code for token
                await ExchangeAndApplyTokenAsync(gitlabUrl, clientId, clientSecret, code, redirectUri);
            }
            catch (TimeoutException)
            {
                ListeningBanner.Visibility = Visibility.Collapsed;
                AppendLog("Authorization timed out (5 minutes). Try again or use the manual fallback below.", LogLevel.Error);
            }
            catch (Exception ex)
            {
                ListeningBanner.Visibility = Visibility.Collapsed;
                AppendLog($"Authorization failed: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        // Manual code entry removed - the loopback listener handles OAuth callback automatically.

        /// <summary>Shared logic: exchange code, validate, display, push to Duo, start auto-refresh.</summary>
        private async System.Threading.Tasks.Task ExchangeAndApplyTokenAsync(
            string gitlabUrl, string clientId, string clientSecret, string code, string redirectUri)
        {
            _lastUsedRedirectUri = redirectUri;

            AppendLog($"Authorization code: {code.Substring(0, Math.Min(10, code.Length))}...", LogLevel.Info);
            AppendLog("Exchanging code for access token...", LogLevel.Info);

            var (success, error) = await _tokenService.ExchangeCodeAsync(gitlabUrl, clientId, clientSecret, code, redirectUri);

            if (!success)
            {
                AppendLog($"Token exchange failed: {error}", LogLevel.Error);
                return;
            }

            AppendLog($"Access token received: {_tokenService.AccessToken?.Substring(0, Math.Min(15, _tokenService.AccessToken?.Length ?? 0))}...", LogLevel.Success);

            // Validate
            AppendLog("Validating token...", LogLevel.Info);
            var valid = await _tokenService.ValidateAsync(gitlabUrl);
            if (valid)
                AppendLog($"Authenticated as: {_tokenService.UserName} (@{_tokenService.UserLogin})", LogLevel.Success);
            else
                AppendLog("Token validation returned an error (token may still work).", LogLevel.Warning);

            // All UI and VS service calls must happen on the main thread.
            // After await, we may not be on the UI thread, so marshal everything via Dispatcher.
            await Dispatcher.InvokeAsync(() =>
            {
                // Show token in UI with copy button
                ShowTokenDisplay();

                // Push to GitLab Duo settings (requires main thread for VS services)
                PushTokenToDuoSettings();

                // Save credentials to .gitconfig
                SaveCredentialsToGitConfig(gitlabUrl, clientId, clientSecret);

                // Start auto-refresh
                StartAutoRefreshTimer();

                // Update UI
                UpdateStatusDisplay();
            });

            AppendLog("Token generated and applied to GitLab Duo settings.", LogLevel.Success);
        }

        private void OnCopyTokenClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_tokenService.AccessToken))
            {
                Clipboard.SetText(_tokenService.AccessToken);
                AppendLog("Token copied to clipboard.", LogLevel.Success);
            }
        }

        private bool _secretRevealed = false;

        private void OnToggleSecretClick(object sender, RoutedEventArgs e)
        {
            _secretRevealed = !_secretRevealed;

            if (_secretRevealed)
            {
                // Show the secret in plain text
                ClientSecretRevealed.Text = ClientSecretInput.Password;
                ClientSecretRevealed.Visibility = Visibility.Visible;
                ClientSecretInput.Visibility = Visibility.Collapsed;
                SecretToggleIcon.Text = "🔒";
            }
            else
            {
                // Hide the secret
                ClientSecretRevealed.Visibility = Visibility.Collapsed;
                ClientSecretInput.Visibility = Visibility.Visible;
                SecretToggleIcon.Text = "👁";
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await RefreshTokenAsync();
        }

        private async System.Threading.Tasks.Task RefreshTokenAsync()
        {
            if (!_tokenService.CanRefresh)
            {
                AppendLog("No refresh token available. Re-authorize.", LogLevel.Error);
                return;
            }

            // Use the same redirect URI pattern for refresh (GitLab requires it to match)
            var redirectUri = _lastUsedRedirectUri ?? "http://127.0.0.1:8585/callback";

            AppendLog("Refreshing token...", LogLevel.Info);

            var (success, error) = await _tokenService.RefreshAsync(
                _currentGitLabUrl, _currentClientId, _currentClientSecret, redirectUri);

            if (!success)
            {
                AppendLog($"Refresh failed: {error}", LogLevel.Error);
                await Dispatcher.InvokeAsync(() => SetStatusBanner("⚠️", "Refresh Failed", error, StatusKind.Error));
                return;
            }

            AppendLog($"Token refreshed: {_tokenService.AccessToken?.Substring(0, Math.Min(15, _tokenService.AccessToken?.Length ?? 0))}...", LogLevel.Success);

            // Validate
            var valid = await _tokenService.ValidateAsync(_currentGitLabUrl);
            if (valid)
                AppendLog($"Validated as: {_tokenService.UserName} (@{_tokenService.UserLogin})", LogLevel.Success);

            // Show token and push to GitLab Duo settings (must be on UI thread)
            await Dispatcher.InvokeAsync(() =>
            {
                ShowTokenDisplay();
                PushTokenToDuoSettings();
                UpdateStatusDisplay();
            });

            AppendLog("Token refreshed and applied to GitLab Duo settings.", LogLevel.Success);
        }

        // ---- Token Display ----

        private void ShowTokenDisplay()
        {
            if (string.IsNullOrEmpty(_tokenService.AccessToken))
            {
                TokenDisplayBanner.Visibility = Visibility.Collapsed;
                return;
            }

            TokenDisplayBanner.Visibility = Visibility.Visible;
            TokenDisplayText.Text = _tokenService.AccessToken;
        }

        // ---- GitLab Duo Integration ----

        private void PushTokenToDuoSettings()
        {
            if (string.IsNullOrEmpty(_tokenService.AccessToken)) return;

            try
            {
                if (!GitLabDuoSettingsService.IsGitLabExtensionAvailable())
                {
                    AppendLog("GitLab extension not found. Copy the token above and set it manually: Tools > Options > GitLab > General > Access Token.", LogLevel.Warning);
                    DuoSettingsStatus.Text = "⚠️ GitLab extension not detected. Use the Copy button above to set the token manually.";
                    DuoSettingsStatus.Foreground = new SolidColorBrush(Colors.IndianRed);
                    return;
                }

                // Set both token and URL atomically, then reload once
                var (tokenSet, urlSet) = GitLabDuoSettingsService.SetTokenAndUrl(_tokenService.AccessToken, _currentGitLabUrl);

                if (tokenSet)
                    AppendLog("Access Token written to GitLab Duo settings store.", LogLevel.Success);
                else
                    AppendLog("Could not write Access Token to settings store.", LogLevel.Warning);

                if (urlSet)
                    AppendLog($"GitLab URL written: {_currentGitLabUrl}", LogLevel.Success);
                else
                    AppendLog("Could not write GitLab URL to settings store.", LogLevel.Warning);

                if (tokenSet || urlSet)
                    AppendLog("GitLab extension reloaded (Load + Save triggered).", LogLevel.Info);

                // Show status based on whether the set succeeded
                if (tokenSet && urlSet)
                {
                    AppendLog("Verified: Token and URL applied to GitLab Duo settings.", LogLevel.Success);
                    DuoSettingsStatus.Text = "✅ Token applied to GitLab Duo settings. Duo Agent should activate automatically.";
                    DuoSettingsStatus.Foreground = new SolidColorBrush(Colors.MediumSeaGreen);
                }
                else if (tokenSet)
                {
                    AppendLog("Token applied but URL could not be set.", LogLevel.Warning);
                    DuoSettingsStatus.Text = "✅ Token applied. URL may need manual configuration in Tools > Options > GitLab.";
                    DuoSettingsStatus.Foreground = new SolidColorBrush(Colors.MediumSeaGreen);
                }
                else
                {
                    AppendLog("Could not apply token to GitLab Duo settings. Copy it manually using the button above.", LogLevel.Warning);
                    DuoSettingsStatus.Text = "⚠️ Auto-apply failed. Use the Copy button and paste into Tools > Options > GitLab > General > Access Token.";
                    DuoSettingsStatus.Foreground = new SolidColorBrush(Colors.IndianRed);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Could not update GitLab Duo settings: {ex.Message}", LogLevel.Warning);
                DuoSettingsStatus.Text = $"⚠️ Error: {ex.Message}. Use the Copy button above.";
                DuoSettingsStatus.Foreground = new SolidColorBrush(Colors.IndianRed);
            }
        }

        // ---- Auto-Refresh Timer ----

        private void StartAutoRefreshTimer()
        {
            _refreshTimer?.Stop();

            // Refresh 5 minutes before expiry
            var interval = _tokenService.ExpiresIn > 300
                ? TimeSpan.FromSeconds(_tokenService.ExpiresIn - 300)
                : TimeSpan.FromMinutes(115);

            _refreshTimer = new DispatcherTimer { Interval = interval };
            _refreshTimer.Tick += async (_, __) =>
            {
                AppendLog("Auto-refresh triggered.", LogLevel.Info);
                await RefreshTokenAsync();
            };
            _refreshTimer.Start();

            AutoRefreshBanner.Visibility = Visibility.Visible;
            AutoRefreshText.Text = $"Auto-refresh active (every {FormatTimeSpan(interval)})";

            AppendLog($"Auto-refresh scheduled: every {FormatTimeSpan(interval)}", LogLevel.Info);
        }

        // ---- .gitconfig ----

        private void SaveCredentialsToGitConfig(string gitlabUrl, string clientId, string clientSecret)
        {
            try
            {
                GitConfigService.WriteCredentials(gitlabUrl, clientId, clientSecret);
                AppendLog("Credentials saved to .gitconfig", LogLevel.Info);
            }
            catch (Exception ex)
            {
                AppendLog($"Could not save to .gitconfig: {ex.Message}", LogLevel.Warning);
            }
        }

        // ---- UI Helpers ----

        private void UpdateStatusDisplay()
        {
            // Status banner is always visible (shows "Not Authorized" when no token)
            if (!_tokenService.HasToken)
            {
                SetStatusBanner("⚪", "Not Authorized", "Click Authorize to generate a token.", StatusKind.Warning);
                TokenCreatedText.Text = "";
                ExpiryText.Text = "";
                RefreshButton.IsEnabled = false;
                return;
            }

            RefreshButton.IsEnabled = _tokenService.CanRefresh;

            // Show token creation datetime
            TokenCreatedText.Text = _tokenService.CreatedAt.HasValue
                ? $"Created: {_tokenService.CreatedAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
                : "";

            if (_tokenService.IsExpired)
            {
                SetStatusBanner("⏱", "Token Expired", "Click Refresh or re-authorize.", StatusKind.Error);
                ExpiryText.Text = _tokenService.ExpiresAt.HasValue
                    ? $"Expired at: {_tokenService.ExpiresAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
                    : "";
            }
            else
            {
                var remaining = _tokenService.TimeUntilExpiry;
                var user = !string.IsNullOrEmpty(_tokenService.UserLogin)
                    ? $"{_tokenService.UserName} (@{_tokenService.UserLogin})" : "";

                SetStatusBanner("✅", "Token Active", !string.IsNullOrEmpty(user) ? $"Authenticated as: {user}" : "", StatusKind.Success);
                ExpiryText.Text = remaining.HasValue
                    ? $"Expires in {FormatTimeSpan(remaining.Value)} (at {_tokenService.ExpiresAt!.Value.ToLocalTime():HH:mm:ss})"
                    : "";
            }
        }

        private enum StatusKind { Success, Warning, Error }

        private void SetStatusBanner(string icon, string title, string detail, StatusKind kind)
        {
            StatusIcon.Text = icon;
            StatusText.Text = title;
            StatusDetail.Text = detail;

            // Use VS theme brushes for the banner background; tint the title text for semantic color
            StatusBanner.Background = FindVsBrush(VsBrushes.InfoBackgroundKey);

            StatusText.Foreground = kind switch
            {
                StatusKind.Success => FindVsBrush(VsBrushes.InfoTextKey),
                StatusKind.Warning => FindVsBrush(VsBrushes.InfoTextKey),
                StatusKind.Error => new SolidColorBrush(Colors.IndianRed),
                _ => FindVsBrush(VsBrushes.InfoTextKey)
            };
        }

        private Brush FindVsBrush(object resourceKey)
        {
            return TryFindResource(resourceKey) as Brush
                ?? SystemColors.ControlTextBrush;
        }

        private void SetButtonsEnabled(bool enabled)
        {
            AuthorizeButton.IsEnabled = enabled;
            RefreshButton.IsEnabled = enabled && _tokenService.CanRefresh;
        }

        private enum LogLevel { Info, Success, Warning, Error }

        private void AppendLog(string message, LogLevel level)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var prefix = level switch
                {
                    LogLevel.Success => "✅",
                    LogLevel.Error => "❌",
                    LogLevel.Warning => "⚠️",
                    _ => "ℹ️"
                };
                LogOutput.Text += $"[{timestamp}] {prefix} {message}\n";
            });
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m";
            return $"{(int)ts.TotalSeconds}s";
        }
    }
}
