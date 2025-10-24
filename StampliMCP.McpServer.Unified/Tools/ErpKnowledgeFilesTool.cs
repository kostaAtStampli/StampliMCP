using System;
using System.Collections.Generic;
using System.Reflection;
using StampliMCP.McpServer.Unified.Services;

namespace StampliMCP.McpServer.Unified.Tools;

internal static class KnowledgeFilesHelper
{
    internal static KnowledgeFilesReport BuildReport(string erp, ErpRegistry registry)
    {
        using var facade = registry.GetFacade(erp);
        var assembly = facade.Knowledge.GetType().Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        var files = new List<KnowledgeFileEntry>();
        foreach (var resource in resourceNames)
        {
            if (!resource.Contains(".Knowledge.", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = resource;
            try
            {
                var idx = resource.IndexOf(".Knowledge.", StringComparison.Ordinal);
                if (idx > 0)
                {
                    fileName = resource[(idx + ".Knowledge.".Length)..]
                        .Replace(".operations.", "operations/")
                        .Replace(".kotlin.", "kotlin/");
                }
            }
            catch
            {
                // Keep original resource name when trimming fails
            }

            files.Add(new KnowledgeFileEntry(resource, fileName));
        }

        return new KnowledgeFilesReport(erp, files);
    }
}

internal sealed record KnowledgeFilesReport(string Erp, IReadOnlyList<KnowledgeFileEntry> Files)
{
    public int TotalFiles => Files.Count;
}

internal sealed record KnowledgeFileEntry(string ResourceName, string FileName);
