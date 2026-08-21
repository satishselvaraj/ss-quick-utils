using OAuthTokenGen.Models;
using OAuthTokenGen.Services;

namespace OAuthTokenGen;

/// <summary>
/// GitLab OAuth Token Generator
/// 
/// This console application automates the process of generating an OAuth 2.0 access token
/// for use with GitLab (including the Visual Studio GitLab extension).
/// 
/// Uses the Authorization Code flow (response_type=code):
///   1. User provides GitLab domain, Application ID, and Application Secret
///   2. App launches browser for OAuth authorization
///   3. GitLab redirects back with an authorization code (?code=...)
///   4. App exchanges the code for an access token via POST /oauth/token
///   5. Token is validated against the GitLab API
///   6. Token is displayed for the user to paste into Visual Studio
/// </summary>
public class Program
{
    private const string Banner = """

        ╔══════════════════════════════════════════════════════════════╗
        ║          GitLab OAuth Token Generator for Visual Studio     ║
        ║                                                              ║
        ║  Generates an OAuth 2.0 token to use in the Visual Studio   ║
        ║  GitLab extension's Access Token (PAT) field.               ║
        ║                                                              ║
        ║  Uses Authorization Code flow (response_type=code)          ║
        ╚══════════════════════════════════════════════════════════════╝
        """;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine(Banner);

        // Determine mode: automated (with local callback server) or manual
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Choose a mode:");
        Console.ResetColor();
        Console.WriteLine("    [1] Automatic - Opens browser & captures code via local server");
        Console.WriteLine("    [2] Manual    - Generates the URL; you paste the redirect URL back");
        Console.WriteLine();
        Console.Write("  Enter choice (1 or 2): ");

        var choice = Console.ReadLine()?.Trim();
        Console.WriteLine();

        return choice switch
        {
            "1" => await RunAutomaticFlow(),
            "2" => await RunManualFlow(),
            _ => await RunAutomaticFlow() // default to automatic
        };
    }

    /// <summary>
    /// Automatic flow: starts a local HTTP server, opens the browser,
    /// captures the authorization code, and exchanges it for an access token.
    /// </summary>
    private static async Task<int> RunAutomaticFlow()
    {
        var config = CollectConfiguration(useLocalServer: true);
        var validation = config.Validate();
        if (!validation.IsValid)
        {
            WriteError(validation.ErrorMessage);
            return 1;
        }

        const int port = 8585;
        config.RedirectUri = $"http://localhost:{port}/";

        using var server = new LocalCallbackServer(port);
        var authorizeUrl = config.BuildAuthorizeUrl();

        PrintStep(1, "Starting local callback server", $"Listening on {server.CallbackUrl}");
        PrintStep(2, "Opening browser for authorization", "Please log in and click 'Authorize'");

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n  URL: {authorizeUrl}\n");
        Console.ResetColor();

        BrowserLauncher.OpenUrl(authorizeUrl);

        PrintStep(3, "Waiting for authorization code", "Timeout: 120 seconds");

        var authorizationCode = await server.WaitForCallbackAsync(TimeSpan.FromSeconds(120));

        if (string.IsNullOrEmpty(authorizationCode))
        {
            WriteError("No authorization code was received. Please try the manual flow instead.");
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  Authorization code received: {authorizationCode[..Math.Min(10, authorizationCode.Length)]}...");
        Console.ResetColor();

        // Exchange the authorization code for an access token
        PrintStep(4, "Exchanging code for access token", "Calling POST /oauth/token...");

        using var exchangeService = new TokenExchangeService();
        var tokenResult = await exchangeService.ExchangeCodeForTokenAsync(config, authorizationCode);

        if (!tokenResult.Success)
        {
            WriteError(tokenResult.ErrorMessage);
            return 1;
        }

        PrintTokenResult(tokenResult);
        await ValidateAndPrintResult(config.GitLabDomain, tokenResult.AccessToken);
        PrintVisualStudioInstructions(config.GitLabDomain, tokenResult.AccessToken);

        return 0;
    }

    /// <summary>
    /// Manual flow: generates the authorization URL, user copies the redirect URL back,
    /// then exchanges the code for a token.
    /// </summary>
    private static async Task<int> RunManualFlow()
    {
        var config = CollectConfiguration(useLocalServer: false);
        var validation = config.Validate();
        if (!validation.IsValid)
        {
            WriteError(validation.ErrorMessage);
            return 1;
        }

        var authorizeUrl = config.BuildAuthorizeUrl();

        PrintStep(1, "Authorization URL generated", "Open this URL in your browser:");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  {authorizeUrl}\n");
        Console.ResetColor();

        Console.Write("  Open in browser now? (y/n): ");
        if (Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true)
        {
            BrowserLauncher.OpenUrl(authorizeUrl);
        }

        PrintStep(2, "Authenticate & Authorize",
            "Log in to GitLab and click 'Authorize'.\n" +
            "  Your browser will redirect to a URL like:\n" +
            "  http://localhost/?code=AUTHORIZATION_CODE_HERE");

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("  Paste the full redirect URL (or just the code): ");
        Console.ResetColor();

        var input = Console.ReadLine()?.Trim() ?? string.Empty;
        Console.WriteLine();

        // Extract the code - user may paste the full URL or just the code
        var authorizationCode = ExtractCodeFromInput(input);

        if (string.IsNullOrEmpty(authorizationCode))
        {
            WriteError("Could not extract authorization code from the input.");
            return 1;
        }

        // Exchange the authorization code for an access token
        PrintStep(3, "Exchanging code for access token", "Calling POST /oauth/token...");

        using var exchangeService = new TokenExchangeService();
        var tokenResult = await exchangeService.ExchangeCodeForTokenAsync(config, authorizationCode);

        if (!tokenResult.Success)
        {
            WriteError(tokenResult.ErrorMessage);
            return 1;
        }

        PrintTokenResult(tokenResult);
        await ValidateAndPrintResult(config.GitLabDomain, tokenResult.AccessToken);
        PrintVisualStudioInstructions(config.GitLabDomain, tokenResult.AccessToken);

        return 0;
    }

    /// <summary>
    /// Extracts the authorization code from user input.
    /// Accepts either a full redirect URL (http://localhost/?code=XYZ) or just the code string.
    /// </summary>
    private static string? ExtractCodeFromInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // If it looks like a URL, parse the code query parameter
        if (input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(input);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var code = query["code"];
                if (!string.IsNullOrEmpty(code))
                    return code;
            }
            catch
            {
                // Fall through to treat as raw code
            }
        }

        // Otherwise treat the entire input as the code
        return input;
    }

    /// <summary>
    /// Collects GitLab domain, Application ID, and Application Secret from the user.
    /// </summary>
    private static OAuthConfig CollectConfiguration(bool useLocalServer)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  ── Step 0: Configuration ──────────────────────────────────\n");
        Console.ResetColor();

        Console.Write("  GitLab URL (e.g., https://gitlab.com): ");
        var domain = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.Write("  OAuth Application ID (client_id):      ");
        var appId = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.Write("  OAuth Application Secret:              ");
        var appSecret = ReadSecretLine();

        Console.WriteLine();

        return new OAuthConfig
        {
            GitLabDomain = domain,
            ApplicationId = appId,
            ApplicationSecret = appSecret,
            RedirectUri = useLocalServer ? "http://localhost:8585/" : "http://localhost"
        };
    }

    /// <summary>
    /// Reads a line from the console while masking input with asterisks.
    /// </summary>
    private static string ReadSecretLine()
    {
        var secret = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && secret.Length > 0)
            {
                secret.Remove(secret.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Backspace)
            {
                secret.Append(key.KeyChar);
                Console.Write('*');
            }
        }
        return secret.ToString();
    }

    /// <summary>
    /// Validates the token against the GitLab API and prints the result.
    /// </summary>
    private static async Task ValidateAndPrintResult(string gitlabDomain, string accessToken)
    {
        PrintStep(4, "Validating token against GitLab API", "");

        using var validator = new TokenValidator();
        var (isValid, message) = await validator.ValidateTokenAsync(gitlabDomain, accessToken);

        if (isValid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  {message.Replace("\n", "\n  ")}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  Warning: {message}");
            Console.WriteLine("  The token may still work. Proceed with caution.");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints the extracted token details.
    /// </summary>
    private static void PrintTokenResult(TokenResult result)
    {
        PrintStep(3, "Token extracted successfully", "");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n  ┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                    TOKEN DETAILS                         │");
        Console.WriteLine("  ├─────────────────────────────────────────────────────────┤");
        Console.ResetColor();

        Console.Write("  │  Access Token : ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        // Show first 20 chars and mask the rest for security
        var masked = result.AccessToken.Length > 20
            ? result.AccessToken[..20] + new string('*', result.AccessToken.Length - 20)
            : result.AccessToken;
        Console.WriteLine(masked.PadRight(40) + "│");
        Console.ResetColor();

        Console.WriteLine($"  │  Token Type   : {result.TokenType,-40}│");
        Console.WriteLine($"  │  Scope        : {result.Scope,-40}│");
        Console.WriteLine($"  │  Expires In   : {result.ExpiresIn + " seconds",-40}│");

        if (result.ExpiresAt.HasValue)
            Console.WriteLine($"  │  Expires At   : {result.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC,-40}│");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  └─────────────────────────────────────────────────────────┘");
        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>
    /// Prints instructions for injecting the token into Visual Studio.
    /// </summary>
    private static void PrintVisualStudioInstructions(string gitlabDomain, string accessToken)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ══════════════════════════════════════════════════════════");
        Console.WriteLine("   VISUAL STUDIO SETUP INSTRUCTIONS");
        Console.WriteLine("  ══════════════════════════════════════════════════════════");
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("  Follow these steps to configure the GitLab extension:");
        Console.WriteLine();
        Console.WriteLine("    1. Open Visual Studio IDE");
        Console.WriteLine("    2. Go to  Tools > Options > GitLab");
        Console.WriteLine("    3. Paste the following into the Access Token field:");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"       {accessToken}");
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine($"    4. Set the GitLab URL to: {gitlabDomain}");
        Console.WriteLine("    5. Click OK / Apply to save the changes");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ✓ Done! The GitLab extension will now use your OAuth token.");
        Console.ResetColor();
        Console.WriteLine();

        // Offer to copy to clipboard
        Console.Write("  Copy token to clipboard? (y/n): ");
        if (Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                CopyToClipboard(accessToken);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ Token copied to clipboard!");
                Console.ResetColor();
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Could not copy to clipboard. Please copy the token manually.");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.WriteLine("  Press any key to exit...");
        Console.ReadKey(true);
    }

    /// <summary>
    /// Copies text to the system clipboard (Windows-specific via PowerShell).
    /// </summary>
    private static void CopyToClipboard(string text)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
        {
            var escaped = text.Replace("\"", "`\"");
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-command \"Set-Clipboard -Value '{escaped}'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(3000);
        }
    }

    private static void PrintStep(int step, string title, string description)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  ── Step {step}: {title} ──");
        Console.ResetColor();

        if (!string.IsNullOrEmpty(description))
            Console.WriteLine($"  {description}");
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n  ✗ Error: {message}");
        Console.ResetColor();
    }
}
