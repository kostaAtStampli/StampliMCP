using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Models;
using System.Collections.Generic;
using System.Linq;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class MatchingConfigurationProvider
{
    private const string ResourceName = "StampliMCP.McpServer.Acumatica.Module.Knowledge.matching.json";
    private readonly ILogger<MatchingConfigurationProvider> _logger;

    public FlowMatchingConfiguration Configuration { get; }

    public MatchingConfigurationProvider(ILogger<MatchingConfigurationProvider> logger)
    {
        _logger = logger;
        Configuration = LoadConfiguration();
    }

    private FlowMatchingConfiguration LoadConfiguration()
    {
        try
        {
            var assembly = typeof(MatchingConfigurationProvider).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                _logger.LogWarning("Matching configuration resource '{ResourceName}' not found. Using defaults.", ResourceName);
                return FlowMatchingConfiguration.CreateDefault();
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var config = JsonSerializer.Deserialize<FlowMatchingConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? FlowMatchingConfiguration.CreateDefault();

            Normalize(config);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load matching configuration. Falling back to defaults.");
            return FlowMatchingConfiguration.CreateDefault();
        }
    }

    private static void Normalize(FlowMatchingConfiguration configuration)
    {
        var defaults = FlowMatchingConfiguration.CreateDefault();

        if (configuration.ActionWords is null || configuration.ActionWords.Count == 0)
        {
            configuration.ActionWords = defaults.ActionWords;
        }
        else
        {
            configuration.ActionWords = configuration.ActionWords
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (configuration.EntityWords is null || configuration.EntityWords.Count == 0)
        {
            configuration.EntityWords = defaults.EntityWords;
        }
        else
        {
            configuration.EntityWords = configuration.EntityWords
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var aliasSource = configuration.Aliases is { Count: > 0 }
            ? configuration.Aliases
            : defaults.Aliases;

        var normalizedAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in aliasSource)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            normalizedAliases[key.Trim()] = value.Trim();
        }

        foreach (var entity in configuration.EntityWords)
        {
            if (!normalizedAliases.ContainsKey(entity))
            {
                normalizedAliases[entity] = entity;
            }
        }

        configuration.Aliases = normalizedAliases;
    }
}
