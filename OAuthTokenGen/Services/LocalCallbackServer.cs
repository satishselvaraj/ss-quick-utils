using System.Net;
using System.Text;

namespace OAuthTokenGen.Services;

/// <summary>
/// A lightweight local HTTP server that listens on http://localhost:{port}/
/// to capture the OAuth authorization code from the redirect query string.
/// 
/// With the Authorization Code flow, GitLab redirects to:
///   http://localhost:8585/?code=AUTHORIZATION_CODE
/// The code is in the query string (not a fragment), so the server receives it directly.
/// The server then serves a success HTML page and returns the code to the caller.
/// </summary>
public class LocalCallbackServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly int _port;

    public int Port => _port;
    public string CallbackUrl => $"http://localhost:{_port}/";

    public LocalCallbackServer(int port = 8585)
    {
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
    }

    /// <summary>
    /// Starts the local server and waits for the OAuth callback.
    /// Returns the authorization code from the ?code= query parameter.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the callback.</param>
    public async Task<string?> WaitForCallbackAsync(TimeSpan timeout)
    {
        _listener.Start();

        using var cts = new CancellationTokenSource(timeout);

        try
        {
            // Single request: GitLab redirects with ?code=... in the query string
            var context = await GetContextAsync(cts.Token);

            // Extract the authorization code from the query string
            var code = context.Request.QueryString["code"];
            var error = context.Request.QueryString["error"];
            var errorDescription = context.Request.QueryString["error_description"];

            if (!string.IsNullOrEmpty(error))
            {
                await ServeErrorPage(context, error, errorDescription ?? "Unknown error");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  OAuth error: {error} - {errorDescription}");
                Console.ResetColor();
                return null;
            }

            if (string.IsNullOrEmpty(code))
            {
                await ServeErrorPage(context, "missing_code", "No authorization code received.");
                return null;
            }

            await ServeSuccessPage(context);
            return code;
        }
        catch (OperationCanceledException)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nTimeout waiting for OAuth callback.");
            Console.ResetColor();
            return null;
        }
        finally
        {
            _listener.Stop();
        }
    }

    /// <summary>
    /// Serves an HTML success page after the authorization code is captured.
    /// </summary>
    private static async Task ServeSuccessPage(HttpListenerContext context)
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <title>GitLab OAuth - Authorization Successful</title>
                <style>
                    body {
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        display: flex; justify-content: center; align-items: center;
                        min-height: 100vh; margin: 0;
                        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                        color: #fff;
                    }
                    .card {
                        background: rgba(255,255,255,0.95); color: #333;
                        border-radius: 12px; padding: 40px; max-width: 500px;
                        box-shadow: 0 20px 60px rgba(0,0,0,0.3); text-align: center;
                    }
                    .success { color: #28a745; font-size: 48px; }
                    h2 { margin: 10px 0; }
                    p { color: #666; }
                </style>
            </head>
            <body>
                <div class="card">
                    <div class="success">&#10003;</div>
                    <h2>Authorization Successful!</h2>
                    <p>The authorization code has been captured.<br>
                    You can close this browser tab and return to the console.</p>
                </div>
            </body>
            </html>
            """;

        await WriteResponse(context, html);
    }

    /// <summary>
    /// Serves an HTML error page when authorization fails.
    /// </summary>
    private static async Task ServeErrorPage(HttpListenerContext context, string error, string description)
    {
        var encodedError = System.Net.WebUtility.HtmlEncode(error);
        var encodedDescription = System.Net.WebUtility.HtmlEncode(description);

        var html = "<!DOCTYPE html>" +
            "<html><head><title>GitLab OAuth - Error</title>" +
            "<style>" +
            "body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; " +
            "display: flex; justify-content: center; align-items: center; " +
            "min-height: 100vh; margin: 0; " +
            "background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%); color: #fff; } " +
            ".card { background: rgba(255,255,255,0.95); color: #333; " +
            "border-radius: 12px; padding: 40px; max-width: 500px; " +
            "box-shadow: 0 20px 60px rgba(0,0,0,0.3); text-align: center; } " +
            ".error { color: #e74c3c; font-size: 48px; } " +
            "h2 { margin: 10px 0; } p { color: #666; } " +
            "code { background: #f0f0f0; padding: 2px 8px; border-radius: 4px; }" +
            "</style></head><body>" +
            "<div class='card'>" +
            "<div class='error'>&#10007;</div>" +
            "<h2>Authorization Failed</h2>" +
            $"<p><strong>Error:</strong> <code>{encodedError}</code></p>" +
            $"<p>{encodedDescription}</p>" +
            "<p>Please close this tab and try again in the console.</p>" +
            "</div></body></html>";

        await WriteResponse(context, html);
    }

    private static async Task WriteResponse(HttpListenerContext context, string html)
    {
        var buffer = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();
    }

    private async Task<HttpListenerContext> GetContextAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<HttpListenerContext>();
        ct.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                tcs.TrySetResult(ctx);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, ct);

        return await tcs.Task;
    }

    public void Dispose()
    {
        _listener.Close();
        GC.SuppressFinalize(this);
    }
}
