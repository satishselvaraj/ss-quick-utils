using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace OAuthTokenGenExt
{
    /// <summary>
    /// VS Package that registers the Tool Window and menu command.
    /// This is the main entry point for the extension.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(TokenGeneratorToolWindow), Style = VsDockStyle.Tabbed, Window = "3ae79031-e1bc-11d0-8f78-00a0c9110057")]
    public sealed class OAuthTokenGenExtPackage : AsyncPackage
    {
        public const string PackageGuidString = "b8f3c1a2-4d5e-6f78-9a0b-c1d2e3f4a5b6";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await OpenTokenGeneratorCommand.InitializeAsync(this);
        }
    }
}
