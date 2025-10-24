using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

namespace StampliMCP.McpServer.Unified.Services;

/// <summary>
/// Helper that builds typed elicitation requests while gracefully degrading when the client or SDK
/// does not support elicitation.
/// </summary>
internal static class ElicitationCompat
{
    internal record Field(string Name, string Kind, string? Description = null, string[]? Options = null);

    internal readonly record struct ElicitationResult(bool Supported, string? Action, IReadOnlyDictionary<string, JsonElement>? Content);

    /// <summary>
    /// Issue an elicitation request using the typed Model Context Protocol API.
    /// When elicitation is unavailable or the client declines, Supported is false.
    /// </summary>
    internal static async Task<ElicitationResult> TryElicitAsync(
        IMcpServer server,
        string message,
        IEnumerable<Field> fields,
        CancellationToken ct)
    {
        var fieldList = fields?.ToList() ?? new List<Field>();
        if (fieldList.Count == 0)
        {
            return new ElicitationResult(false, null, null);
        }

        try
        {
            var request = BuildRequest(message, fieldList);
            if (request is null)
            {
                return new ElicitationResult(false, null, null);
            }

            var response = await server.ElicitAsync(request, ct).ConfigureAwait(false);
            Log.Debug("Elicitation response: action={Action}", response.Action);

            IReadOnlyDictionary<string, JsonElement>? content = response.Content is null
                ? null
                : response.Content.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return new ElicitationResult(true, response.Action, content);
        }
        catch (Exception ex)
        {
            Log.Debug("Elicitation skipped: {Message}", ex.Message);
            return new ElicitationResult(false, null, null);
        }
    }

    private static ElicitRequestParams? BuildRequest(string message, IReadOnlyList<Field> fields)
    {
        var schema = new ElicitRequestParams.RequestSchema();
        foreach (var field in fields)
        {
            var kind = field.Kind?.Trim().ToLowerInvariant();
            ElicitRequestParams.PrimitiveSchemaDefinition definition = kind switch
            {
                "boolean" => new ElicitRequestParams.BooleanSchema { Description = field.Description },
                "number" => new ElicitRequestParams.NumberSchema { Description = field.Description },
                _ when field.Options is { Length: > 0 } => new ElicitRequestParams.EnumSchema
                {
                    Description = field.Description,
                    Enum = field.Options!.ToList()
                },
                _ => new ElicitRequestParams.StringSchema { Description = field.Description }
            };

            schema.Properties[field.Name] = definition;
        }

        if (schema.Properties.Count == 0)
        {
            return null;
        }

        return new ElicitRequestParams
        {
            Message = message,
            RequestedSchema = schema
        };
    }
}
