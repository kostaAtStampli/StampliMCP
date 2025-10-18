using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Resources;

/// <summary>
/// MCP Resource server for Acumatica knowledge.
/// Serves embedded knowledge files via URIs for AI reference.
/// Example: stampli://operations/vendors, stampli://kotlin/golden
/// </summary>
[McpServerResourceType]
public class AcumaticaResources
{
    private static readonly Assembly _assembly = typeof(AcumaticaResources).Assembly;

    [McpServerResource(UriTemplate = "stampli://operations/{category}", Name = "Operations by Category", MimeType = "application/json")]
    [Description("Get all operations for a specific category (vendors, items, payments, etc.)")]
    public static TextResourceContents GetOperations(string category)
    {
        try
        {
            var resourceName = category switch
            {
                "vendors" => "StampliMCP.McpServer.Acumatica.Knowledge.vendor-operations.json",
                "items" => "StampliMCP.McpServer.Acumatica.Knowledge.item-operations.json",
                "payments" => "StampliMCP.McpServer.Acumatica.Knowledge.payment-operations.json",
                "purchaseOrders" => "StampliMCP.McpServer.Acumatica.Knowledge.po-operations.json",
                "accounts" => "StampliMCP.McpServer.Acumatica.Knowledge.account-operations.json",
                "fields" => "StampliMCP.McpServer.Acumatica.Knowledge.field-operations.json",
                "admin" => "StampliMCP.McpServer.Acumatica.Knowledge.admin-operations.json",
                _ => $"StampliMCP.McpServer.Acumatica.Knowledge.operations.{category}.json"
            };

            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return new TextResourceContents
                {
                    Uri = $"stampli://operations/{category}",
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(new { error = $"Category '{category}' not found" })
                };
            }

            using var reader = new StreamReader(stream);
            return new TextResourceContents
            {
                Uri = $"stampli://operations/{category}",
                MimeType = "application/json",
                Text = reader.ReadToEnd()
            };
        }
        catch (Exception ex)
        {
            return new TextResourceContents
            {
                Uri = $"stampli://operations/{category}",
                MimeType = "application/json",
                Text = JsonSerializer.Serialize(new { error = ex.Message })
            };
        }
    }

    [McpServerResource(UriTemplate = "stampli://flows/{flowName}", Name = "Integration Flow", MimeType = "application/json")]
    [Description("Get detailed flow information (vendor_export, payment, standard_import, etc.)")]
    public static TextResourceContents GetFlow(string flowName)
    {
        try
        {
            var resourceName = $"StampliMCP.McpServer.Acumatica.Knowledge.flows.{flowName.ToLower().Replace("_", "_")}.json";

            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return new TextResourceContents
                {
                    Uri = $"stampli://flows/{flowName}",
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(new { error = $"Flow '{flowName}' not found" })
                };
            }

            using var reader = new StreamReader(stream);
            return new TextResourceContents
            {
                Uri = $"stampli://flows/{flowName}",
                MimeType = "application/json",
                Text = reader.ReadToEnd()
            };
        }
        catch (Exception ex)
        {
            return new TextResourceContents
            {
                Uri = $"stampli://flows/{flowName}",
                MimeType = "application/json",
                Text = JsonSerializer.Serialize(new { error = ex.Message })
            };
        }
    }

    [McpServerResource(UriTemplate = "stampli://kotlin/golden", Name = "Kotlin Golden Reference", MimeType = "text/markdown")]
    [Description("Get the Kotlin golden reference implementation (exportVendor - the ONLY Kotlin operation)")]
    public static TextResourceContents GetKotlinGoldenReference()
    {
        try
        {
            var resourceName = "StampliMCP.McpServer.Acumatica.Knowledge.kotlin.GOLDEN_PATTERNS.md";

            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return new TextResourceContents
                {
                    Uri = "stampli://kotlin/golden",
                    MimeType = "text/plain",
                    Text = "Kotlin golden reference not found"
                };
            }

            using var reader = new StreamReader(stream);
            return new TextResourceContents
            {
                Uri = "stampli://kotlin/golden",
                MimeType = "text/markdown",
                Text = reader.ReadToEnd()
            };
        }
        catch (Exception ex)
        {
            return new TextResourceContents
            {
                Uri = "stampli://kotlin/golden",
                MimeType = "text/plain",
                Text = $"Error loading Kotlin golden reference: {ex.Message}"
            };
        }
    }

    [McpServerResource(UriTemplate = "stampli://kotlin/tdd-workflow", Name = "TDD Workflow Guide", MimeType = "text/markdown")]
    [Description("Get the TDD workflow methodology for implementing Kotlin operations")]
    public static TextResourceContents GetTddWorkflow()
    {
        try
        {
            var resourceName = "StampliMCP.McpServer.Acumatica.Knowledge.kotlin.TDD_WORKFLOW.md";

            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return new TextResourceContents
                {
                    Uri = "stampli://kotlin/tdd-workflow",
                    MimeType = "text/plain",
                    Text = "TDD workflow guide not found"
                };
            }

            using var reader = new StreamReader(stream);
            return new TextResourceContents
            {
                Uri = "stampli://kotlin/tdd-workflow",
                MimeType = "text/markdown",
                Text = reader.ReadToEnd()
            };
        }
        catch (Exception ex)
        {
            return new TextResourceContents
            {
                Uri = "stampli://kotlin/tdd-workflow",
                MimeType = "text/plain",
                Text = $"Error loading TDD workflow: {ex.Message}"
            };
        }
    }

    [McpServerResource(UriTemplate = "stampli://categories", Name = "All Categories", MimeType = "application/json")]
    [Description("Get list of all available operation categories")]
    public static TextResourceContents GetCategories()
    {
        try
        {
            var resourceName = "StampliMCP.McpServer.Acumatica.Knowledge.categories.json";

            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return new TextResourceContents
                {
                    Uri = "stampli://categories",
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(new { error = "Categories not found" })
                };
            }

            using var reader = new StreamReader(stream);
            return new TextResourceContents
            {
                Uri = "stampli://categories",
                MimeType = "application/json",
                Text = reader.ReadToEnd()
            };
        }
        catch (Exception ex)
        {
            return new TextResourceContents
            {
                Uri = "stampli://categories",
                MimeType = "application/json",
                Text = JsonSerializer.Serialize(new { error = ex.Message })
            };
        }
    }
}