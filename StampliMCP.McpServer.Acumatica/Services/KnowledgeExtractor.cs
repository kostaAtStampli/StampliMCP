using System.Text.Json;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Parses logs from code scanning and extracts knowledge to update JSON files
/// </summary>
public class KnowledgeExtractor
{
    public ExtractedKnowledge ParseLogs(string scan1LogPath, string scan2LogPath)
    {
        var scan1Json = ExtractJsonFromLog(scan1LogPath);
        var scan2Json = ExtractJsonFromLog(scan2LogPath);

        var scan1Data = JsonDocument.Parse(scan1Json);
        var scan2Data = JsonDocument.Parse(scan2Json);

        return new ExtractedKnowledge
        {
            RequiredFields = ParseRequiredFields(scan2Data), // Trust Scan 2
            ScanThese = ParseScanThese(scan1Data, scan2Data),
            Helpers = ParseHelpers(scan1Data),
            KotlinFiles = ParseKotlinFiles(scan2Data),
            KotlinProgress = ParseKotlinProgress(scan2Data),
            Scan2Challenges = CountChallenges(scan1Data, scan2Data)
        };
    }

    public int CountChallenges(JsonDocument scan1, JsonDocument scan2)
    {
        // Count how many findings differed between Scan 1 and Scan 2
        // For now, just return 0 - we can implement detailed comparison later
        // The important thing is that Scan 2 happened and we have the logs
        return 0;
    }

    public void UpdateKnowledgeFile(string filePath, ExtractedKnowledge knowledge)
    {
        // Create backup
        CreateBackup(filePath);

        // Load existing JSON
        var existingJson = JsonDocument.Parse(File.ReadAllText(filePath));

        // Merge knowledge (preserve descriptions, update pointers)
        var merged = MergeKnowledge(existingJson, knowledge);

        // Save
        File.WriteAllText(filePath, JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void CreateBackup(string filePath)
    {
        if (File.Exists(filePath))
        {
            var backupPath = $"{filePath}.backup";
            File.Copy(filePath, backupPath, overwrite: true);
            Console.WriteLine($"✓ Backup created: {backupPath}");
        }
    }

    private string ExtractJsonFromLog(string logPath)
    {
        var content = File.ReadAllText(logPath);

        // Find JSON in STDOUT section
        var stdoutStart = content.IndexOf("=== STDOUT", StringComparison.Ordinal);
        if (stdoutStart == -1)
        {
            throw new InvalidOperationException($"No STDOUT section in log: {logPath}");
        }

        var stderrStart = content.IndexOf("=== STDERR", StringComparison.Ordinal);
        var stdoutContent = stderrStart > stdoutStart
            ? content.Substring(stdoutStart, stderrStart - stdoutStart)
            : content.Substring(stdoutStart);

        // Find first { or [
        var json = "";
        for (int i = 0; i < stdoutContent.Length; i++)
        {
            if (stdoutContent[i] == '{' || stdoutContent[i] == '[')
            {
                json = stdoutContent.Substring(i).Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(json))
        {
            throw new InvalidOperationException($"No JSON found in log: {logPath}");
        }

        // Strip trailing markdown fence if present (JSON may end with ```)
        var closingFence = json.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence > 0)
        {
            // Remove everything from closing fence onwards
            json = json.Substring(0, closingFence).Trim();
        }

        // DEBUG: Write extracted JSON to file for inspection
        var debugPath = $"{logPath}.extracted.json";
        File.WriteAllText(debugPath, json);
        Console.WriteLine($"DEBUG: Extracted JSON written to {debugPath} ({json.Length} chars)");

        return json;
    }

    private Dictionary<string, object> ParseRequiredFields(JsonDocument scan2)
    {
        var fields = new Dictionary<string, object>();

        // Parse from Scan 2 (trusted source)
        if (scan2.RootElement.TryGetProperty("allValidations", out var validations))
        {
            foreach (var validation in validations.EnumerateArray())
            {
                if (validation.TryGetProperty("field", out var fieldProp))
                {
                    var fieldName = fieldProp.GetString() ?? "";
                    var type = validation.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "unknown";
                    var value = validation.TryGetProperty("value", out var valueProp) ? valueProp.GetString() : null;

                    fields[fieldName] = new { type, maxLength = value };
                }
            }
        }

        return fields;
    }

    private List<FilePointer> ParseScanThese(JsonDocument scan1, JsonDocument scan2)
    {
        var pointers = new List<FilePointer>();

        // Use Scan 2 verified line numbers if available, otherwise Scan 1
        // Implementation depends on JSON structure

        return pointers;
    }

    private List<Helper> ParseHelpers(JsonDocument scan1)
    {
        var helpers = new List<Helper>();
        // Parse from scan1
        return helpers;
    }

    private List<KotlinFile> ParseKotlinFiles(JsonDocument scan2)
    {
        var files = new List<KotlinFile>();

        // Scan2 has structure: { "Q1_KOTLIN_INFRASTRUCTURE_VERIFICATION": { "allKotlinFiles": [...] } }
        // Navigate to Q1 first
        if (scan2.RootElement.TryGetProperty("Q1_KOTLIN_INFRASTRUCTURE_VERIFICATION", out var q1))
        {
            if (q1.TryGetProperty("allKotlinFiles", out var kotlinFiles))
            {
                foreach (var file in kotlinFiles.EnumerateArray())
                {
                    files.Add(new KotlinFile
                    {
                        File = file.TryGetProperty("file", out var f) ? f.GetString() ?? "" : "",
                        Path = file.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "",
                        Lines = file.TryGetProperty("lines", out var l) ? l.GetString() : null,
                        Purpose = file.TryGetProperty("purpose", out var pu) ? pu.GetString() : null
                    });
                }
            }
        }

        return files;
    }

    private string ParseKotlinProgress(JsonDocument scan2)
    {
        // Parse Kotlin migration status from scan2
        if (scan2.RootElement.TryGetProperty("overridden", out var overridden) &&
            scan2.RootElement.TryGetProperty("total", out var total))
        {
            var overriddenCount = overridden.GetArrayLength();
            var totalCount = total.GetInt32();
            return $"{overriddenCount} methods migrated to Kotlin, {totalCount - overriddenCount} still in Java";
        }

        return "Unknown progress";
    }

    private object MergeKnowledge(JsonDocument existing, ExtractedKnowledge knowledge)
    {
        // Build updated knowledge structure
        var updated = new Dictionary<string, object>
        {
            ["purpose"] = "GOLDEN REFERENCE for Kotlin migration patterns - use exportVendor as teaching example",
            ["usage"] = "When implementing ANY operation in Kotlin, scan this reference FIRST to learn patterns",

            ["goldenExample"] = new
            {
                operation = "exportVendor",
                status = "COMPLETE - Use as reference for all future Kotlin migrations",
                why = "First operation migrated from Java to Kotlin - demonstrates all key patterns"
            },

            // FIX PATH: kotlin-drivers → finsys-modern
            ["kotlinSkeletonPath"] = "C:\\STAMPLI4\\core\\finsys-modern\\kotlin-acumatica-driver\\",
            ["javaLegacyPath"] = "C:\\STAMPLI4\\core\\finsys-drivers\\acumatica\\",
            ["pathNote"] = "Use Windows paths (C:\\) - MCP server runs as .exe, NOT WSL paths (/mnt/c/)",

            ["architecture"] = new
            {
                pattern = "Incremental Override Pattern",
                description = "Kotlin driver extends Java driver, overrides methods one at a time via TDD",
                @class = "class KotlinAcumaticaDriver : AcumaticaDriver()",
                delegation = "All non-overridden methods automatically delegate to Java parent",
                @interface = "IDualFinsysDriver (~50 methods total)",
                currentProgress = knowledge.KotlinProgress ?? "1 method migrated (exportVendor), 49 still in Java"
            },

            // UPDATE: Replace 4 files with all 14 from Scan 2
            ["goldenReferenceFiles"] = knowledge.KotlinFiles.Select(f =>
            {
                // Try to preserve existing keyPatterns if file matches
                var existingFile = GetExistingFileData(existing, f.File);

                return new Dictionary<string, object>
                {
                    ["file"] = f.Path ?? "",
                    ["lines"] = f.Lines ?? "unknown",
                    ["purpose"] = f.Purpose ?? "Extracted from Scan 2",
                    ["keyPatterns"] = TryGetArrayOrDefault(existingFile, "keyPatterns"),
                    ["javaComparison"] = TryGetStringOrDefault(existingFile, "javaComparison"),
                    ["comparison"] = TryGetStringOrDefault(existingFile, "comparison")
                };
            }).ToArray()
        };

        // Add critical issue found by Scan 2
        if (knowledge.KotlinFiles.Count > 10)
        {
            updated["criticalIssues"] = new[]
            {
                "Package mismatch: com.stampli.finsys.modern.acumatica vs directory structure com/stampli/kotlin/acumatica/driver",
                $"Knowledge update: Found {knowledge.KotlinFiles.Count} Kotlin files (was tracking 4)"
            };
        }

        return updated;
    }

    private JsonElement? GetExistingFileData(JsonDocument existing, string fileName)
    {
        try
        {
            if (existing.RootElement.TryGetProperty("goldenReferenceFiles", out var files))
            {
                foreach (var file in files.EnumerateArray())
                {
                    if (file.TryGetProperty("file", out var fileProp))
                    {
                        var path = fileProp.GetString() ?? "";
                        if (path.Contains(fileName))
                        {
                            return file;
                        }
                    }
                }
            }
        }
        catch
        {
            // If can't find, return null
        }
        return null;
    }

    private List<string> TryGetArrayOrDefault(JsonElement? element, string propertyName)
    {
        var list = new List<string>();
        if (element.HasValue && element.Value.TryGetProperty(propertyName, out var prop))
        {
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    list.Add(item.GetString() ?? "");
                }
            }
        }
        return list;
    }

    private string TryGetStringOrDefault(JsonElement? element, string propertyName)
    {
        if (element.HasValue && element.Value.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetString() ?? "";
        }
        return "";
    }
}
