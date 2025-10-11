namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class SearchService(KnowledgeService knowledge)
{
    public async Task<List<(string Operation, string Match)>> SearchAsync(string query, CancellationToken ct = default)
    {
        var results = new List<(string, string)>();
        var categories = await knowledge.GetCategoriesAsync(ct);

        foreach (var category in categories)
        {
            var ops = await knowledge.GetOperationsByCategoryAsync(category.Name, ct);
            
            foreach (var op in ops.Where(o => 
                o.Method.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                o.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add((op.Method, $"{op.Summary[..Math.Min(80, op.Summary.Length)]}..."));
            }
        }

        return results;
    }
}

