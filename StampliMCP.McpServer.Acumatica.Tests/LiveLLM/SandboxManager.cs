using System;
using System.IO;

namespace StampliMCP.McpServer.Acumatica.Tests.LiveLLM;

/// <summary>
/// Manages temporary sandbox directories for LLM tests
/// Ensures clean creation and deletion of test workspaces
/// </summary>
public sealed class SandboxManager : IDisposable
{
    private readonly string _baseDir;
    private readonly List<string> _createdDirs = new();
    private bool _disposed;

    public SandboxManager()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "mcp_llm_tests");
        Directory.CreateDirectory(_baseDir);
    }

    /// <summary>
    /// Create a new sandbox directory for a test
    /// </summary>
    public string CreateSandbox(string testName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var sandbox = Path.Combine(_baseDir, $"{testName}_{timestamp}_{Guid.NewGuid().ToString().Substring(0, 8)}");

        Directory.CreateDirectory(sandbox);
        _createdDirs.Add(sandbox);

        // Create standard Kotlin project structure
        Directory.CreateDirectory(Path.Combine(sandbox, "src", "main", "kotlin"));
        Directory.CreateDirectory(Path.Combine(sandbox, "src", "test", "kotlin"));

        return sandbox;
    }

    /// <summary>
    /// Clean up a specific sandbox
    /// </summary>
    public void CleanupSandbox(string sandboxPath)
    {
        if (Directory.Exists(sandboxPath))
        {
            try
            {
                Directory.Delete(sandboxPath, recursive: true);
                _createdDirs.Remove(sandboxPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to delete sandbox {sandboxPath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Clean up all created sandboxes
    /// </summary>
    public void CleanupAll()
    {
        foreach (var dir in _createdDirs.ToList())
        {
            CleanupSandbox(dir);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        CleanupAll();

        // Try to remove base directory if empty
        try
        {
            if (Directory.Exists(_baseDir) && !Directory.EnumerateFileSystemEntries(_baseDir).Any())
            {
                Directory.Delete(_baseDir);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        _disposed = true;
    }
}
