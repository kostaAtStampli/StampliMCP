using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

/// <summary>
/// Tool that reads Kotlin golden reference files (exportVendor implementation) and returns
/// the actual file content + extracted patterns. This guarantees Kotlin context is available
/// without relying on Claude to scan files.
/// </summary>
[McpServerToolType]
public static class GetKotlinGoldenReferenceTool
{
    [McpServerTool(
        Name = "get_kotlin_golden_reference",
        Title = "Kotlin Golden Reference",
        UseStructuredContent = true
    )]
    [Description(@"
MANDATORY PREREQUISITE for kotlin_tdd_workflow tool.

Returns the Kotlin golden reference implementation (exportVendor - the ONLY Kotlin operation migrated so far).
This tool reads actual Kotlin source files and returns their content so you can learn Kotlin patterns.

⚠️ CRITICAL: Call this tool BEFORE calling kotlin_tdd_workflow when implementing any Acumatica operation.

Returns:
- Full source code of 3 Kotlin files (KotlinAcumaticaDriver.kt, CreateVendorHandler.kt, VendorPayloadMapper.kt)
- Extracted Kotlin patterns catalog (null safety, Handler pattern, object singletons, etc.)
- Acumatica-specific quirks (never throw exceptions, pagination limits, session management)

Default operation assumption: NOT yet implemented in Kotlin (delegates to Java parent).
Use exportVendor patterns to implement new Kotlin operations.
")]
    public static async Task<ModelContextProtocol.Protocol.CallToolResult> Execute(
        AcumaticaKnowledgeService knowledge,
        CancellationToken ct = default)
    {
        try
        {
            Serilog.Log.Information("Tool {Tool} started: Reading Kotlin golden reference files", "get_kotlin_golden_reference");

            // GUARANTEED FILE READING - C# does this, not Claude
            // NOTE: MCP server runs as Windows .exe, so use Windows paths (C:\) not WSL paths (/mnt/c/)
            // CORRECTED PATH: finsys-modern module structure
            var basePath = @"C:\STAMPLI4\core\finsys-modern\kotlin-acumatica-driver\src\main\kotlin\com\stampli\kotlin\acumatica\driver";

            var driverPath = Path.Combine(basePath, "KotlinAcumaticaDriver.kt");
            var handlerPath = Path.Combine(basePath, "vendor", "CreateVendorHandler.kt");
            var mapperPath = Path.Combine(basePath, "vendor", "VendorPayloadMapper.kt");

            // Read all 3 files
            var driver = await File.ReadAllTextAsync(driverPath, ct);
            Serilog.Log.Information("DEBUG: Read {File} - {Size} chars", "KotlinAcumaticaDriver.kt", driver.Length);

            var handler = await File.ReadAllTextAsync(handlerPath, ct);
            Serilog.Log.Information("DEBUG: Read {File} - {Size} chars", "CreateVendorHandler.kt", handler.Length);

            var mapper = await File.ReadAllTextAsync(mapperPath, ct);
            Serilog.Log.Information("DEBUG: Read {File} - {Size} chars", "VendorPayloadMapper.kt", mapper.Length);

            // Get the extracted patterns from embedded knowledge
            var patterns = await knowledge.GetKotlinGoldenReferenceAsync(ct);
            Serilog.Log.Information("DEBUG: Loaded Kotlin patterns from knowledge service");

            var result = new
            {
                goldenOperation = "exportVendor",
                status = "ONLY Kotlin operation implemented - use as teaching example",
                defaultAssumption = "All other operations NOT in Kotlin - delegate to Java AcumaticaDriver parent",

                kotlinFiles = new[]
                {
                    new
                    {
                        file = "KotlinAcumaticaDriver.kt",
                        path = driverPath,
                        purpose = "Main driver - shows override pattern, Handler delegation, error handling",
                        lines = "1-27",
                        content = driver,
                        keyPatterns = new[]
                        {
                            "class KotlinAcumaticaDriver : AcumaticaDriver()",
                            "override fun exportVendor(request: ExportVendorRequest): ExportVendorResponse",
                            "val handler = CreateVendorHandler()",
                            "return handler.handle(request, apiCallerFactory, connectionManager)",
                            "response.error = e.message (property syntax, not setError())",
                            "try-catch with error in response.error, never throw exceptions"
                        }
                    },
                    new
                    {
                        file = "CreateVendorHandler.kt",
                        path = handlerPath,
                        purpose = "Handler pattern - shows null safety (!!, ?., let), early returns, Kotlin idioms",
                        lines = "1-132",
                        content = handler,
                        keyPatterns = new[]
                        {
                            "internal class CreateVendorHandler",
                            "companion object { private val logger = Logger.getLogger(...) }",
                            "if (raw == null) { response.error = \"...\"; return response }",
                            "val validName = name!! (null assertion)",
                            "raw[\"id\"]?.takeIf { it.toString().isNotBlank() }.let { id -> ... }",
                            "string interpolation: ${e.message}",
                            "JSONObject().apply { put(key, value) }"
                        }
                    },
                    new
                    {
                        file = "VendorPayloadMapper.kt",
                        path = mapperPath,
                        purpose = "Object singleton - shows data object, extension functions, apply/let usage",
                        lines = "1-71",
                        content = mapper,
                        keyPatterns = new[]
                        {
                            "internal data object VendorPayloadMapper (singleton with no state)",
                            "fun JSONObject.withValue(v: String) = apply { } (extension function)",
                            "const val STAMPLI_LINK_PREFIX (compile-time constant)",
                            "JSONObject().apply { put(...); put(...) } (scope function for initialization)"
                        }
                    }
                },

                criticalQuirks = new
                {
                    kotlinSpecific = new[]
                    {
                        "Use 'data object' for singletons with no state (not 'object')",
                        "Property access: response.error = \"message\" (NOT setError())",
                        "Null safety: !!, ?., let, takeIf, elvis operator ?:",
                        "Scope functions: apply for initialization, let for null-safe chaining",
                        "companion object for static-like members (logger, constants)",
                        "lateinit var for properties initialized in @BeforeEach (tests)"
                    },
                    acumaticaSpecific = new[]
                    {
                        "NEVER throw exceptions - always return error in response.error field",
                        "RESPONSE_ROWS_LIMIT = 2000 (pagination page size)",
                        "TIME_LIMIT = 10 minutes (session refresh during long imports)",
                        "AcumaticaImportHelper template pattern from Java (reuse infrastructure)",
                        "AcumaticaAuthenticator.authenticatedApiCall() wrapper (login-try-finally-logout)",
                        "hasNextPage() returns true if responseRowsCount == pageSize (more pages exist)"
                    }
                },

                extractedPatterns = patterns,

                instructions = new[]
                {
                    "SCAN all 3 file contents above to learn Kotlin syntax and patterns",
                    "STUDY the keyPatterns array for each file to find critical code examples",
                    "APPLY these patterns when implementing new Kotlin operations",
                    "REMEMBER: Most operations NOT in Kotlin yet - exportVendor is the golden reference",
                    "ALWAYS use try-catch with response.error, NEVER throw exceptions",
                    "REUSE Java infrastructure: ApiCallerFactory, AcumaticaImportHelper, AcumaticaAuthenticator"
                }
            };

            // DEBUG: Serialize result to check total size
            var resultJson = System.Text.Json.JsonSerializer.Serialize(result);
            Serilog.Log.Information("DEBUG: GetKotlinGoldenReferenceTool result object serialized to {Size} chars JSON",
                resultJson.Length);

            Serilog.Log.Information("Tool {Tool} completed: {DriverSize} + {HandlerSize} + {MapperSize} chars loaded",
                "get_kotlin_golden_reference", driver.Length, handler.Length, mapper.Length);

            var ret = new ModelContextProtocol.Protocol.CallToolResult();
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result });
            
            // Serialize full golden reference (including full Kotlin source code) as JSON for LLM consumption
            var jsonOutput = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            ret.Content.Add(new ModelContextProtocol.Protocol.TextContentBlock { Type = "text", Text = jsonOutput });

            ret.Content.Add(new ModelContextProtocol.Protocol.ResourceLinkBlock
            {
                Uri = "mcp://stampli-unified/kotlin_tdd_workflow",
                Name = "Run Kotlin TDD workflow",
                Description = "Use the workflow after reviewing the golden reference"
            });
            ret.Content.Add(new ModelContextProtocol.Protocol.ResourceLinkBlock
            {
                Uri = "mcp://stampli-unified/erp__list_erps",
                Name = "List registered ERPs",
                Description = "Verify available ERP modules and capabilities"
            });
            return ret;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}", "get_kotlin_golden_reference", ex.Message);
            var ret = new ModelContextProtocol.Protocol.CallToolResult();
            var errorObj = new
            {
                error = $"Failed to load Kotlin golden reference: {ex.Message}",
                note = "Check that Kotlin files exist at C:\\STAMPLI4\\core\\finsys-modern\\kotlin-acumatica-driver\\...",
                expectedFiles = new[]
                {
                    "src/main/kotlin/com/stampli/kotlin/acumatica/driver/KotlinAcumaticaDriver.kt",
                    "src/main/kotlin/com/stampli/kotlin/acumatica/driver/vendor/CreateVendorHandler.kt",
                    "src/main/kotlin/com/stampli/kotlin/acumatica/driver/vendor/VendorPayloadMapper.kt"
                }
            };
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = errorObj });
            
            // Serialize error details as JSON for LLM consumption
            var jsonOutput = System.Text.Json.JsonSerializer.Serialize(errorObj, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            ret.Content.Add(new ModelContextProtocol.Protocol.TextContentBlock { Type = "text", Text = jsonOutput });
            return ret;
        }
    }
}
