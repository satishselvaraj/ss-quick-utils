var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on all interfaces (required for Docker)
// Use PORT env variable if set, otherwise default to 8585
var port = Environment.GetEnvironmentVariable("PORT") ?? "8585";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Serve index.html as the default file
app.UseDefaultFiles();

// Serve static files from wwwroot (mapped from public/)
app.UseStaticFiles();

Console.WriteLine();
Console.WriteLine("  =============================================");
Console.WriteLine("  GitLab OAuth Token Generator V3");
Console.WriteLine("  Static Web App running on .NET Kestrel");
Console.WriteLine("  =============================================");
Console.WriteLine();
Console.WriteLine($"  URL: http://localhost:{port}/");
Console.WriteLine();
Console.WriteLine("  Press Ctrl+C to stop the server.");
Console.WriteLine();

app.Run();
