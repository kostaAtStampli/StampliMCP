var builder = DistributedApplication.CreateBuilder(args);

// Configure API service with health checks
var apiService = builder.AddProject<Projects.StampliMCP_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

// Add unified MCP server (stdio-based, not HTTP)
// Note: MCP uses stdio transport, not HTTP, so no health checks or lifecycle management apply
var mcpUnified = builder.AddProject<Projects.StampliMCP_McpServer_Unified>("mcp-unified");

// Configure web frontend with dependencies and health checks
builder.AddProject<Projects.StampliMCP_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService); // Aspire 9.5: WaitFor ensures proper startup ordering

// Build and run the distributed application
builder.Build().Run();

// Note: Single-file AppHost feature requires additional configuration
// and is currently experimental in Aspire 9.5
