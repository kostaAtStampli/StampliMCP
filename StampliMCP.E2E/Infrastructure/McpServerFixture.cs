using System.Diagnostics;
using ModelContextProtocol.Client;

namespace StampliMCP.E2E.Infrastructure;

public sealed class McpServerFixture : IAsyncLifetime, IDisposable
{
    public McpClient? Client { get; private set; }
    public string PublishDir { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        PublishDir = await PublishUnifiedAsync();
        var exe = Path.Combine(PublishDir, "stampli-mcp-unified.exe");
        var transport = new StdioClientTransport(new()
        {
            Command = exe,
            Arguments = Array.Empty<string>()
        });
        Client = await McpClient.CreateAsync(transport);
    }

    public async ValueTask DisposeAsync()
    {
        // Properly dispose client to cleanup transport and kill server process
        if (Client is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (Client is IDisposable disposable)
        {
            disposable.Dispose();
        }
        Client = null;
    }

    public void Dispose() { /* no-op */ }


    private static async Task<string> PublishUnifiedAsync()
    {
        var repoRoot = GetRepoRoot();
        var csproj = Path.Combine(repoRoot, "StampliMCP.McpServer.Unified", "StampliMCP.McpServer.Unified.csproj");

        var psi = new ProcessStartInfo
        {
            FileName = DotnetPath(),
            Arguments = $"publish \"{csproj}\" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true --nologo",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi)!;
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet publish failed: {stderr}\n{stdout}");
        }

        var pubDir = Path.Combine(repoRoot, "StampliMCP.McpServer.Unified", "bin", "Release", "net10.0", "win-x64", "publish");
        if (!Directory.Exists(pubDir))
            throw new DirectoryNotFoundException($"Publish directory not found: {pubDir}");
        return pubDir;
    }

    private static string GetRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "StampliMCP.slnx"))) return dir;
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        throw new DirectoryNotFoundException("Repo root not found");
    }

    private static string DotnetPath()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidate = Path.Combine(pf, "dotnet", "dotnet.exe");
        return File.Exists(candidate) ? candidate : "dotnet";
    }
}

// xUnit v3 shared collection for reusing server between tests
[CollectionDefinition("MCP-Server")]
public sealed class McpServerCollection : ICollectionFixture<McpServerFixture>
{
}
