using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace OAuthTokenGenExt
{
    /// <summary>
    /// Command to open the Token Generator tool window.
    /// Registered under Tools menu: "GitLab OAuth Token Generator"
    /// </summary>
    internal sealed class OpenTokenGeneratorCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new("c7d8e9f0-1a2b-3c4d-5e6f-708192a3b4c5");

        private readonly AsyncPackage _package;

        private OpenTokenGeneratorCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        public static OpenTokenGeneratorCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new OpenTokenGeneratorCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var window = _package.FindToolWindow(typeof(TokenGeneratorToolWindow), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create tool window");

            var windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}
