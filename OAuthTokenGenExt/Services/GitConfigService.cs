using System;
using System.IO;
using System.Text.RegularExpressions;

namespace OAuthTokenGenExt.Services
{
    /// <summary>
    /// Reads and writes OAuth credentials from/to the user's ~/.gitconfig file.
    /// Looks for the [credential "https://..."] section with gitLabDevClientId and gitLabDevClientSecret.
    /// </summary>
    public static class GitConfigService
    {
        private static readonly string GitConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitconfig");

        public static (string? gitlabUrl, string? clientId, string? clientSecret) ReadCredentials()
        {
            if (!File.Exists(GitConfigPath))
                return (null, null, null);

            var content = File.ReadAllText(GitConfigPath);

            // Match [credential "https://some-gitlab-url"]
            var sectionPattern = new Regex(
                @"\[credential\s+""(https?://[^""]+)""\]\s*\n((?:\s+\w+\s*=\s*[^\n]*\n)*)",
                RegexOptions.IgnoreCase);

            foreach (Match section in sectionPattern.Matches(content))
            {
                var url = section.Groups[1].Value.Trim();
                var body = section.Groups[2].Value;

                var clientId = ExtractValue(body, "gitLabDevClientId");
                var clientSecret = ExtractValue(body, "gitLabDevClientSecret");

                if (!string.IsNullOrEmpty(clientId))
                {
                    // Normalize URL: ensure it has https:// prefix
                    if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        url = "https://" + url;

                    return (url, clientId, clientSecret);
                }
            }

            return (null, null, null);
        }

        public static void WriteCredentials(string gitlabUrl, string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(gitlabUrl) || string.IsNullOrWhiteSpace(clientId))
                return;

            // Normalize URL: strip trailing slash, extract host for credential section
            var uri = new Uri(gitlabUrl.TrimEnd('/'));
            var credentialUrl = $"{uri.Scheme}://{uri.Host}";

            var content = File.Exists(GitConfigPath) ? File.ReadAllText(GitConfigPath) : "";

            // Check if a credential section for this host already exists
            var sectionPattern = new Regex(
                @"\[credential\s+""" + Regex.Escape(credentialUrl) + @"""\]\s*\n((?:\s+\w+\s*=\s*[^\n]*\n)*)",
                RegexOptions.IgnoreCase);

            var match = sectionPattern.Match(content);
            if (match.Success)
            {
                // Update existing section
                var body = match.Groups[1].Value;
                var newBody = SetValue(body, "gitLabDevClientId", clientId);
                newBody = SetValue(newBody, "gitLabDevClientSecret", clientSecret);
                content = content.Substring(0, match.Groups[1].Index)
                    + newBody
                    + content.Substring(match.Groups[1].Index + match.Groups[1].Length);
            }
            else
            {
                // Append new section
                content = content.TrimEnd() + "\n\n"
                    + $"[credential \"{credentialUrl}\"]\n"
                    + $"    gitLabDevClientId = {clientId}\n"
                    + $"    gitLabDevClientSecret = {clientSecret}\n"
                    + $"    gitLabAuthModes = browser\n"
                    + $"    provider = gitlab\n";
            }

            File.WriteAllText(GitConfigPath, content);
        }

        private static string? ExtractValue(string sectionBody, string key)
        {
            var pattern = new Regex($@"^\s*{Regex.Escape(key)}\s*=\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var match = pattern.Match(sectionBody);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        private static string SetValue(string sectionBody, string key, string value)
        {
            var pattern = new Regex($@"^(\s*){Regex.Escape(key)}\s*=\s*.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (pattern.IsMatch(sectionBody))
                return pattern.Replace(sectionBody, $"$1{key} = {value}");
            return sectionBody.TrimEnd('\n') + $"\n    {key} = {value}\n";
        }
    }
}
