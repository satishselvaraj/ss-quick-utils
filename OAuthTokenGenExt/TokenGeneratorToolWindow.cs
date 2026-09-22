using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace OAuthTokenGenExt
{
    /// <summary>
    /// Tool window that hosts the OAuth Token Generator native WPF UI.
    /// Accessible from Tools > GitLab OAuth Token Generator.
    /// </summary>
    [Guid("d1e2f3a4-b5c6-7d8e-9f0a-1b2c3d4e5f60")]
    public class TokenGeneratorToolWindow : ToolWindowPane
    {
        public TokenGeneratorToolWindow() : base(null)
        {
            Caption = "GitLab OAuth Token Generator";
            Content = new TokenGeneratorControl();
        }
    }
}
