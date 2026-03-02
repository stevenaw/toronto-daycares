using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TorontoDaycares;
using TorontoDaycares.Models;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
// Register TorontoDaycares services and HTTP clients used by the DaycareService
builder.Services
    .AddHttpClient<DaycareRepository>();

builder.Services
    .AddHttpClient<GpsRepository>(GpsRepository.ConfigureClient);

builder.Services
    .AddHttpClient<CityWardRepository>();

builder.Services.AddSingleton<DaycareService>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class DaycareTools
{
    [McpServerTool(Name = "ListDaycares")]
    [Description("List daycares with optional filters")]
    public static async Task<DaycareSearchResponse> ListDaycares(
        int topN,
        int[]? wards,
        string[]? programs,
        string? near,
        DaycareSearchOptions options,
        DaycareService service,
        CancellationToken cancellationToken = default)
    {
        // Convert program strings to ProgramType if possible
        ProgramType[]? programTypes = null;
        if (programs is { Length: > 0 })
        {
            var list = new List<ProgramType>();
            foreach (var p in programs)
            {
                if (Enum.TryParse<ProgramType>(p, true, out var pt))
                    list.Add(pt);
            }
            programTypes = list.ToArray();
        }

        var request = new DaycareSearchRequest()
        {
            TopN = topN,
            Wards = wards,
            Programs = programTypes,
            NearAddress = near,
            Options = options,
        };

        return await service.SearchDaycares(request, cancellationToken);
    }

    [McpServerTool(Name = "GetDaycareById")]
    [Description("Get a daycare by numeric id")]
    public static async Task<Daycare?> GetDaycareById(int id, DaycareSearchOptions options, DaycareService service, CancellationToken cancellationToken = default)
    {
        var daycares = await service.GetDaycares(options, cancellationToken);
        return daycares.FirstOrDefault(d => d.Id == id);
    }

    [McpServerTool(Name = "ListWards")]
    [Description("List city wards")]
    public static async Task<CityWard[]> ListWards(CityWardRepository repo)
    {
        return await repo.GetWardsAsync();
    }

    [McpServerTool(Name = "Geocode")]
    [Description("Resolve an address to coordinates")]
    public static async Task<Coordinates?> Geocode(string address, GpsRepository repo)
    {
        return await repo.GetCoordinates(address);
    }
}
