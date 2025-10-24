using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Models;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class FlowSignatureProvider
{
    private const string ResourceName = "StampliMCP.McpServer.Acumatica.Module.Knowledge.flow-signatures.json";
    private readonly ILogger<FlowSignatureProvider> _logger;

    public IReadOnlyList<FlowSignature> Signatures { get; }

    public FlowSignatureProvider(ILogger<FlowSignatureProvider> logger)
    {
        _logger = logger;
        Signatures = LoadSignatures();
    }

    private IReadOnlyList<FlowSignature> LoadSignatures()
    {
        try
        {
            var assembly = typeof(FlowSignatureProvider).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                _logger.LogWarning("Flow signature resource '{ResourceName}' not found. Falling back to defaults.", ResourceName);
                return CreateDefault();
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var catalog = JsonSerializer.Deserialize<FlowSignatureCatalog>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new FlowSignatureCatalog();

            if (catalog.Flows.Count == 0)
            {
                _logger.LogWarning("Flow signature catalog is empty. Using defaults.");
                return CreateDefault();
            }

            return Normalize(catalog.Flows);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load flow signatures. Using defaults.");
            return CreateDefault();
        }
    }

    private static IReadOnlyList<FlowSignature> Normalize(IEnumerable<FlowSignature> flows)
    {
        return flows
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .Select(f => new FlowSignature
            {
                Name = f.Name.Trim(),
                Actions = NormalizeList(f.Actions),
                Entities = NormalizeList(f.Entities),
                Keywords = NormalizeList(f.Keywords)
            })
            .ToList();
    }

    private static List<string> NormalizeList(IEnumerable<string>? values)
    {
        return values is null
            ? new List<string>()
            : values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private static IReadOnlyList<FlowSignature> CreateDefault()
    {
        return new List<FlowSignature>
        {
            new()
            {
                Name = "standard_import_flow",
                Actions = ["import"],
                Entities = [],
                Keywords = ["import", "get", "retrieve", "fetch"]
            }
        };
    }
}
