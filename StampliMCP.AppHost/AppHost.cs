var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.StampliMCP_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

// Add MCP server (stdio-based, not HTTP)
var mcpAcumatica = builder.AddProject<Projects.StampliMCP_McpServer_Acumatica>("mcp-acumatica");

builder.AddProject<Projects.StampliMCP_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
