using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Unified.Services;

/// <summary>
/// Best-effort adapter to the MCP elicitation API without taking a hard compile-time dependency
/// on preview types. Uses reflection to discover ElicitAsync and related schema types at runtime.
/// If elicitation is not available in the current SDK, methods return (false, null).
/// </summary>
internal static class ElicitationCompat
{
    internal record Field(string Name, string Kind, string? Description = null, string[]? Options = null);

    /// <summary>
    /// Try to elicit structured input from the client. Returns Accepted=false when not supported
    /// or user rejected. On success, returns Accepted=true and a read-only map of string->JsonElement.
    /// </summary>
    internal static async Task<(bool Accepted, IReadOnlyDictionary<string, JsonElement>? Content)> TryElicitAsync(
        IMcpServer server,
        string message,
        IEnumerable<Field> fields,
        CancellationToken ct)
    {
        try
        {
            var protocolAsm = typeof(CallToolResult).Assembly; // Protocol assembly present in all versions
            var serverAsm = typeof(IMcpServer).Assembly;

            // Locate types by name to avoid hard dependency on preview namespaces
            var elicitRequestParamsType = protocolAsm.GetType("ModelContextProtocol.Protocol.ElicitRequestParams")
                                          ?? serverAsm.GetType("ModelContextProtocol.Server.ElicitRequestParams");
            var requestSchemaType = protocolAsm.GetType("ModelContextProtocol.Protocol.RequestSchema")
                                    ?? serverAsm.GetType("ModelContextProtocol.Server.RequestSchema");

            if (elicitRequestParamsType is null || requestSchemaType is null)
            {
                return (false, null);
            }

            // Find schema leaf types
            var allTypes = protocolAsm.GetTypes().Concat(serverAsm.GetTypes()).ToArray();
            var boolSchemaType = allTypes.FirstOrDefault(t => t.Name == "BooleanSchema");
            var numberSchemaType = allTypes.FirstOrDefault(t => t.Name == "NumberSchema");
            var stringSchemaType = allTypes.FirstOrDefault(t => t.Name == "StringSchema");

            if (boolSchemaType is null || numberSchemaType is null || stringSchemaType is null)
            {
                return (false, null);
            }

            // Create RequestSchema and populate Properties
            var requestSchema = Activator.CreateInstance(requestSchemaType) ?? throw new InvalidOperationException("Failed to create RequestSchema");
            var propertiesProp = requestSchemaType.GetProperty("Properties")
                                  ?? throw new InvalidOperationException("RequestSchema.Properties not found");
            var properties = propertiesProp.GetValue(requestSchema) as System.Collections.IDictionary
                             ?? throw new InvalidOperationException("RequestSchema.Properties is not a dictionary");

            foreach (var field in fields)
            {
                Type leafType = field.Kind.ToLowerInvariant() switch
                {
                    "boolean" => boolSchemaType,
                    "number" => numberSchemaType,
                    _ => stringSchemaType
                };

                var leaf = Activator.CreateInstance(leafType);
                var descProp = leafType.GetProperty("Description");
                if (descProp is not null && field.Description is not null)
                {
                    descProp.SetValue(leaf, field.Description);
                }

                // Some SDKs expose AllowedValues/Enum on StringSchema; set if present
                if (field.Options is { Length: > 0 })
                {
                    var enumProp = leafType.GetProperty("Enum") ?? leafType.GetProperty("AllowedValues");
                    enumProp?.SetValue(leaf, field.Options);
                }

                properties[field.Name] = leaf!;
            }

            // Build ElicitRequestParams
            var req = Activator.CreateInstance(elicitRequestParamsType) ?? throw new InvalidOperationException("Failed to create ElicitRequestParams");
            var messageProp = elicitRequestParamsType.GetProperty("Message");
            var requestedSchemaProp = elicitRequestParamsType.GetProperty("RequestedSchema");

            messageProp?.SetValue(req, message);
            requestedSchemaProp?.SetValue(req, requestSchema);

            // Find ElicitAsync extension: Task<ElicitResult> ElicitAsync(IMcpServer, ElicitRequestParams, CancellationToken)
            var elicitMethod = serverAsm.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .FirstOrDefault(m => m.Name == "ElicitAsync" && m.GetParameters().Length >= 2);

            if (elicitMethod is null)
            {
                return (false, null);
            }

            var taskObj = elicitMethod.GetParameters().Length switch
            {
                3 => elicitMethod.Invoke(null, new object?[] { server, req, ct }),
                2 => elicitMethod.Invoke(null, new object?[] { server, req }),
                _ => null
            } as Task;

            if (taskObj is null)
            {
                return (false, null);
            }

            await taskObj.ConfigureAwait(false);

            // Read Task.Result via reflection
            var resultProp = taskObj.GetType().GetProperty("Result");
            var result = resultProp?.GetValue(taskObj);
            if (result is null)
            {
                return (false, null);
            }

            var actionProp = result.GetType().GetProperty("Action");
            var contentProp = result.GetType().GetProperty("Content");

            var action = actionProp?.GetValue(result) as string;
            var isAccepted = string.Equals(action, "accept", StringComparison.OrdinalIgnoreCase);
            if (!isAccepted)
            {
                return (false, null);
            }

            var contentObj = contentProp?.GetValue(result);
            if (contentObj is System.Collections.IDictionary dict)
            {
                var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    var key = entry.Key?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(key)) continue;

                    // Expect JsonElement values; if not, serialize back/forth to coerce
                    if (entry.Value is JsonElement je)
                    {
                        map[key] = je;
                    }
                    else
                    {
                        var coerced = JsonSerializer.SerializeToElement(entry.Value);
                        map[key] = coerced;
                    }
                }

                return (true, map);
            }

            return (true, new Dictionary<string, JsonElement>());
        }
        catch
        {
            // On any failure, quietly disable elicitation
            return (false, null);
        }
    }
}
