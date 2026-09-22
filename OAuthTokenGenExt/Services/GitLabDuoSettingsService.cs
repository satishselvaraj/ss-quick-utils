using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace OAuthTokenGenExt.Services
{
    /// <summary>
    /// Reads and writes the GitLab for Visual Studio extension settings.
    ///
    /// Two-step approach:
    /// 1. Write to the VS WritableSettingsStore (persistent storage) at:
    ///    ApplicationPrivateSettings\GitLab.Extension.SettingsUtil.GeneralSettings
    /// 2. Force the GitLab extension to reload via reflection:
    ///    GeneralSettings.Instance.Load() triggers the in-memory singleton to re-read from store
    ///
    /// This ensures the GitLab extension picks up the new token immediately
    /// without requiring VS restart or manual Tools > Options interaction.
    /// </summary>
    public static class GitLabDuoSettingsService
    {
        private const string CollectionPath = @"ApplicationPrivateSettings\GitLab.Extension.SettingsUtil.GeneralSettings";
        private const string ValuePrefix = "1*"; // BaseOptionModel string type prefix
        private const string GeneralSettingsTypeName = "GitLab.Extension.SettingsUtil.GeneralSettings";

        /// <summary>
        /// Set the Access Token only (without triggering reload).
        /// The token is DPAPI-encrypted before storage.
        /// Call ApplyAndReload() after setting both token and URL.
        /// </summary>
        public static bool SetAccessToken(string token)
        {
            var protectedToken = DpapiProtect(token);
            return SetProperty("AccessToken", protectedToken);
        }

        /// <summary>
        /// Set the GitLab URL only (without triggering reload).
        /// The URL is stored as plain text (not encrypted).
        /// Call ApplyAndReload() after setting both token and URL.
        /// </summary>
        public static bool SetGitLabUrl(string url)
        {
            return SetProperty("GitLabUrl", url);
        }

        /// <summary>
        /// Set both AccessToken and GitLabUrl, then force the GitLab extension to pick them up.
        /// 
        /// Two-pronged approach:
        /// 1. Write DPAPI-encrypted token + URL to WritableSettingsStore (persistent)
        /// 2. Directly set the raw token on the Settings singleton via reflection (immediate)
        /// 
        /// The direct-set approach bypasses the Load() path entirely and sets the value
        /// on the in-memory Settings object that the LS client reads from.
        /// </summary>
        public static (bool tokenSet, bool urlSet) SetTokenAndUrl(string token, string gitlabUrl)
        {
            // Mimic exactly what happens when user clicks OK in Tools > Options > GitLab:
            // 1. Set properties on GeneralSettings.Instance (the BaseOptionModel singleton)
            // 2. Call Save() which writes to WritableSettingsStore AND triggers the Saved event
            // 3. The Saved event propagates: Settings re-reads -> SettingsChangedEvent -> LS client -> Duo activates
            var (tokenSet, urlSet) = DirectSetAndSave(token, gitlabUrl);
            return (tokenSet, urlSet);
        }

        /// <summary>
        /// Get the current Access Token from the settings store (returns the raw/unprotected value).
        /// </summary>
        public static string? GetAccessToken()
        {
            var protectedValue = GetProperty("AccessToken");
            if (string.IsNullOrEmpty(protectedValue)) return null;
            return DpapiUnprotect(protectedValue);
        }

        /// <summary>
        /// Get the current GitLab URL from the settings store.
        /// </summary>
        public static string? GetGitLabUrl()
        {
            return GetProperty("GitLabUrl");
        }

        /// <summary>
        /// Check if the VS settings store is accessible.
        /// </summary>
        public static bool IsGitLabExtensionAvailable()
        {
            try
            {
                return GetSettingsStore() != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Mimics exactly what happens when the user clicks OK in Tools > Options > GitLab > General.
        /// 
        /// The Options dialog (BaseOptionPage&lt;GeneralSettings&gt;) does:
        ///   1. Sets properties on GeneralSettings.Instance
        ///   2. Calls GeneralSettings.Instance.Save()
        ///   3. Save() writes all properties to WritableSettingsStore (with "1*" prefix)
        ///   4. Save() fires the static Saved event
        ///   5. The Settings class (DI singleton) subscribes to Saved, re-reads from GeneralSettings
        ///   6. Settings fires SettingsChangedEvent
        ///   7. The LS client subscribes to SettingsChangedEvent, sends DidChangeConfiguration to the Language Server
        ///   8. The Language Server resolves the project from the git remote URL + GitLabUrl
        ///   9. Duo Agent activates with the project selected
        /// 
        /// We replicate steps 1-2 via reflection. Steps 3-9 happen automatically.
        /// 
        /// The AccessToken is DPAPI-encrypted before setting, because the Settings class
        /// calls _protect.Unprotect() when reading it (the getter decrypts, the setter encrypts).
        /// Since we're setting on GeneralSettings directly (bypassing the Settings setter),
        /// we must encrypt ourselves.
        /// </summary>
        private static (bool tokenSet, bool urlSet) DirectSetAndSave(string rawToken, string gitlabUrl)
        {
            bool tokenSet = false, urlSet = false;

            try
            {
                var gitlabAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "GitLab.Extension");

                if (gitlabAsm == null)
                {
                    System.Diagnostics.Debug.WriteLine("DirectSetAndSave: GitLab.Extension assembly not loaded in AppDomain");
                    return (false, false);
                }

                var settingsType = gitlabAsm.GetType(GeneralSettingsTypeName);
                if (settingsType == null)
                {
                    System.Diagnostics.Debug.WriteLine($"DirectSetAndSave: {GeneralSettingsTypeName} type not found");
                    return (false, false);
                }

                // Get the BaseOptionModel<GeneralSettings> singleton
                var instanceProp = settingsType.GetProperty("Instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                var instance = instanceProp?.GetValue(null);
                if (instance == null)
                {
                    System.Diagnostics.Debug.WriteLine("DirectSetAndSave: GeneralSettings.Instance is null");
                    return (false, false);
                }

                // Step 1: Set AccessToken (DPAPI-encrypted, matching ProtectImpl format)
                var protectedToken = DpapiProtect(rawToken);
                var tokenProp = settingsType.GetProperty("AccessToken", BindingFlags.Instance | BindingFlags.Public);
                if (tokenProp != null)
                {
                    tokenProp.SetValue(instance, protectedToken);
                    tokenSet = true;
                    System.Diagnostics.Debug.WriteLine("DirectSetAndSave: AccessToken set on GeneralSettings.Instance");
                }

                // Step 2: Set GitLabUrl (plain text)
                var urlProp = settingsType.GetProperty("GitLabUrl", BindingFlags.Instance | BindingFlags.Public);
                if (urlProp != null)
                {
                    urlProp.SetValue(instance, gitlabUrl);
                    urlSet = true;
                    System.Diagnostics.Debug.WriteLine($"DirectSetAndSave: GitLabUrl set to {gitlabUrl}");
                }

                // Step 3: Call Save() - this does TWO things:
                //   a) Writes all properties to WritableSettingsStore (persistent)
                //   b) Fires the static Saved event (triggers the entire downstream chain)
                var saveMethod = settingsType.GetMethod("Save",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy,
                    null, Type.EmptyTypes, null);
                if (saveMethod != null)
                {
                    saveMethod.Invoke(instance, null);
                    System.Diagnostics.Debug.WriteLine("DirectSetAndSave: Save() called - persisted to store + Saved event fired");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DirectSetAndSave error: {ex.Message}");
            }

            return (tokenSet, urlSet);
        }

        private static bool SetProperty(string propertyName, string value)
        {
            try
            {
                var store = GetSettingsStore();
                if (store == null) return false;

                if (!store.CollectionExists(CollectionPath))
                    store.CreateCollection(CollectionPath);

                store.SetString(CollectionPath, propertyName, ValuePrefix + value);

                System.Diagnostics.Debug.WriteLine($"GitLabDuoSettings.Set({propertyName}) = [set successfully]");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GitLabDuoSettings.Set({propertyName}) error: {ex.Message}");
                return false;
            }
        }

        private static string? GetProperty(string propertyName)
        {
            try
            {
                var store = GetSettingsStore();
                if (store == null) return null;

                if (!store.CollectionExists(CollectionPath))
                    return null;

                if (!store.PropertyExists(CollectionPath, propertyName))
                    return null;

                var raw = store.GetString(CollectionPath, propertyName);

                if (raw != null && raw.StartsWith(ValuePrefix))
                    return raw.Substring(ValuePrefix.Length);

                return raw;
            }
            catch
            {
                return null;
            }
        }

        private static WritableSettingsStore? GetSettingsStore()
        {
            try
            {
                var settingsManagerService = Package.GetGlobalService(typeof(SVsSettingsManager)) as IVsSettingsManager;
                if (settingsManagerService == null)
                {
                    System.Diagnostics.Debug.WriteLine("GetSettingsStore: SVsSettingsManager service not available");
                    return null;
                }

                var settingsManager = new ShellSettingsManager(settingsManagerService);
                return settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSettingsStore error: {ex.Message}");
                return null;
            }
        }

        // ---- DPAPI encryption (matches GitLab extension's ProtectImpl) ----

        /// <summary>
        /// Encrypt a string using DPAPI (CurrentUser scope), matching the GitLab extension's ProtectImpl.Protect().
        /// Returns a Base64-encoded string of the encrypted bytes.
        /// </summary>
        private static string DpapiProtect(string plainText)
        {
            try
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DpapiProtect error: {ex.Message}");
                return plainText; // fallback: store unprotected
            }
        }

        /// <summary>
        /// Decrypt a DPAPI-protected Base64 string back to plain text.
        /// </summary>
        private static string? DpapiUnprotect(string protectedBase64)
        {
            try
            {
                var encryptedBytes = Convert.FromBase64String(protectedBase64);
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // If it's not DPAPI-protected, return as-is (might be a raw token)
                return protectedBase64;
            }
        }
    }
}
