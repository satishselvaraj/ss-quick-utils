using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace OAuthTokenGenExt.Services
{
    /// <summary>
    /// Starts a temporary HTTP listener on a loopback address (127.0.0.1) with a random port
    /// to capture the OAuth redirect callback. The listener serves a single request, extracts
    /// the authorization code from the query string, shows a success page, and shuts down.
    /// No admin access or firewall rules needed for loopback addresses.
    /// </summary>
    public class LoopbackHttpListener : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly TaskCompletionSource<string> _codeReceived = new();
        private readonly CancellationTokenSource _cts = new();

        /// <summary>The full redirect URI including the port (e.g., http://127.0.0.1:52341/callback).</summary>
        public string RedirectUri { get; }

        /// <summary>The port the listener is bound to.</summary>
        public int Port { get; }

        public LoopbackHttpListener()
        {
            // Find a free port by binding to port 0
            var tempListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tempListener.Start();
            Port = ((IPEndPoint)tempListener.LocalEndpoint).Port;
            tempListener.Stop();

            RedirectUri = $"http://127.0.0.1:{Port}/callback";

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        }

        /// <summary>
        /// Start listening and wait for the OAuth callback.
        /// Returns the authorization code, or throws on timeout/cancellation.
        /// </summary>
        public async Task<string> WaitForCallbackAsync(string expectedState, TimeSpan timeout)
        {
            _listener.Start();

            // Set up timeout
            var timeoutCts = new CancellationTokenSource(timeout);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            _ = Task.Run(() => ListenLoop(expectedState, linkedCts.Token), linkedCts.Token);

            try
            {
                // Wait for the code or timeout
                var completedTask = await Task.WhenAny(
                    _codeReceived.Task,
                    Task.Delay(timeout, linkedCts.Token));

                if (completedTask == _codeReceived.Task)
                    return await _codeReceived.Task;

                throw new TimeoutException("OAuth callback was not received within the timeout period.");
            }
            finally
            {
                Stop();
            }
        }

        private async Task ListenLoop(string expectedState, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _listener.IsListening)
                {
                    var contextTask = _listener.GetContextAsync();
                    var completed = await Task.WhenAny(contextTask, Task.Delay(-1, ct));

                    if (completed != contextTask) break;

                    var context = await contextTask;
                    var request = context.Request;
                    var response = context.Response;

                    // Parse the query string
                    var query = HttpUtility.ParseQueryString(request.Url?.Query ?? "");
                    var code = query["code"];
                    var state = query["state"];
                    var error = query["error"];
                    var errorDesc = query["error_description"];

                    if (!string.IsNullOrEmpty(error))
                    {
                        // OAuth error
                        var errorHtml = BuildResponsePage(
                            "Authorization Failed",
                            $"Error: {error}<br/>{errorDesc ?? ""}<br/><br/>You can close this tab.",
                            false);
                        SendResponse(response, errorHtml);
                        _codeReceived.TrySetException(new Exception($"OAuth error: {error} - {errorDesc}"));
                        break;
                    }

                    if (!string.IsNullOrEmpty(code))
                    {
                        // Verify state parameter (CSRF protection)
                        if (!string.IsNullOrEmpty(expectedState) && state != expectedState)
                        {
                            var mismatchHtml = BuildResponsePage(
                                "Security Error",
                                "State parameter mismatch. Possible CSRF attack. Please try again.",
                                false);
                            SendResponse(response, mismatchHtml);
                            _codeReceived.TrySetException(new Exception("OAuth state mismatch. Possible CSRF attack."));
                            break;
                        }

                        // Success - return the code
                        var successHtml = BuildResponsePage(
                            "Authorization Successful!",
                            "The authorization code has been received.<br/>You can close this tab and return to Visual Studio.",
                            true);
                        SendResponse(response, successHtml);
                        _codeReceived.TrySetResult(code);
                        break;
                    }

                    // Unknown request - send a simple response
                    var defaultHtml = BuildResponsePage(
                        "Waiting for Authorization",
                        "Waiting for GitLab to redirect back...",
                        false);
                    SendResponse(response, defaultHtml);
                }
            }
            catch (ObjectDisposedException) { /* listener was stopped */ }
            catch (HttpListenerException) { /* listener was stopped */ }
            catch (OperationCanceledException) { /* cancelled */ }
        }

        private static void SendResponse(HttpListenerResponse response, string html)
        {
            try
            {
                var buffer = Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = 200;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch { /* best effort */ }
        }

        private static string BuildResponsePage(string title, string message, bool isSuccess)
        {
            var color = isSuccess ? "#108548" : "#dd2b0e";
            var icon = isSuccess ? "✅" : "⚠️";
            return $@"<!DOCTYPE html>
<html>
<head>
    <title>{title}</title>
    <style>
        body {{ font-family: 'Segoe UI', sans-serif; background: #1e1e1e; color: #d4d4d4;
               display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; }}
        .card {{ background: #252526; border: 1px solid #3f3f46; border-radius: 12px;
                 padding: 40px; text-align: center; max-width: 500px; box-shadow: 0 4px 24px rgba(0,0,0,0.4); }}
        h1 {{ color: {color}; font-size: 1.4rem; margin-bottom: 12px; }}
        .icon {{ font-size: 3rem; margin-bottom: 16px; }}
        p {{ color: #9d9d9d; font-size: 0.95rem; line-height: 1.6; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='icon'>{icon}</div>
        <h1>{title}</h1>
        <p>{message}</p>
    </div>
</body>
</html>";
        }

        public void Stop()
        {
            try
            {
                _cts.Cancel();
                if (_listener.IsListening)
                    _listener.Stop();
            }
            catch { /* best effort */ }
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
            (_listener as IDisposable)?.Dispose();
        }
    }
}
