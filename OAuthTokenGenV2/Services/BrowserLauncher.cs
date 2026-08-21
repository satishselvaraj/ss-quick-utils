using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OAuthTokenGenV2.Services;

/// <summary>
/// Cross-platform browser launcher utility.
/// Opens the OAuth authorization URL in the user's default browser.
/// </summary>
public static class BrowserLauncher
{
    /// <summary>
    /// Opens the specified URL in the default system browser.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    /// <returns>True if the browser was launched successfully.</returns>
    public static bool OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows requires shell execute with the URL
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                // Fallback: try shell execute
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to open browser: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine("Please manually open the URL in your browser.");
            return false;
        }
    }
}
