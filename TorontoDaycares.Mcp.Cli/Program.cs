using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using TorontoDaycares.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.WriteLine(System.Environment.CurrentDirectory);

Console.WriteLine("Starting MCP client...");
// Configure the transport to run the local MCP server project. Adjust the --project path as needed
var clientTransport = new StdioClientTransport(new()
{
    Name = "TorontoDaycares MCP Server",
    Command = "dotnet",
    Arguments = new[] { "run", "--project", "../../../../TorontoDaycares.Mcp.Server" },
});

var client = await McpClient.CreateAsync(clientTransport);

// Print the list of tools available from the server.
Console.WriteLine("Available tools:");
foreach (var tool in await client.ListToolsAsync())
{
    Console.WriteLine($"  {tool.Name} - {tool.Description}");
}

Console.WriteLine();
Console.WriteLine("Simple CLI commands:");
Console.WriteLine("  list                - list daycares");
Console.WriteLine("  get <id>            - get daycare by numeric id");
Console.WriteLine("  wards               - list city wards");
Console.WriteLine("  exit                - quit");

while (true)
{
    Console.Write("cmd> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
        continue;

    var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    var cmd = parts[0].ToLowerInvariant();

    try
    {
        if (cmd == "exit" || cmd == "quit")
            break;

        if (cmd == "list")
        {
            // Request a plain listing without filters: default topN and options=0
            var callArgs = new Dictionary<string, object?>() { ["topN"] = 50, ["wards"] = null, ["programs"] = null, ["near"] = null, ["options"] = 0 };
            var result = await client.CallToolAsync("ListDaycares", callArgs, cancellationToken: CancellationToken.None);
            if (!result.Content.Any())
            {
                Console.WriteLine("No data returned");
                continue;
            }

            var json = ExtractJsonFromContent(result.Content);
            if (json is not null)
            {
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                jsonOptions.Converters.Add(new JsonStringEnumConverter());
                var resp = JsonSerializer.Deserialize<DaycareSearchResponse>(json, jsonOptions);

                if (resp is not null && resp.TopPrograms != null)
                {
                    var items = resp!.TopPrograms.GroupBy(x => x.Program.ProgramType).ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var programType in items)
                    {
                        var title = $"Top {programType.Value.Count} {programType.Key} programs:";
                        Console.WriteLine(title);
                        Console.WriteLine(new string('-', title.Length));

                        foreach (var item in programType.Value)
                        {
                            Console.Write($"{item.Program.Rating?.ToString("0.00") ?? "n/a"} / 5 - {item.Daycare.Name} - {item.Daycare.Address}");
                            if (!string.IsNullOrEmpty(item.Daycare.NearestIntersection))
                                Console.Write($" ({item.Daycare.NearestIntersection})");
                            Console.WriteLine();
                        }

                        Console.WriteLine();
                    }
                }
                else
                {
                    // Fallback: print raw content blocks
                    PrintRawResult(result);
                }
            }
        }
        else if (cmd == "get")
        {
            var rest = parts.Length == 2 ? parts[1].Trim() : string.Empty;
            // If a single numeric id is provided, fetch that daycare
            if (int.TryParse(rest, out var id))
            {
                var result = await client.CallToolAsync("GetDaycareById", new Dictionary<string, object?>() { ["id"] = id, ["options"] = 0 }, cancellationToken: CancellationToken.None);
                var json = ExtractJsonFromContent(result.Content);
                if (json is null)
                {
                    Console.WriteLine("Not found");
                }
                else
                {
                    try
                    {
                        var d = System.Text.Json.JsonSerializer.Deserialize<TorontoDaycares.Models.Daycare>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (d is null)
                            Console.WriteLine("Not found");
                        else
                            PrintDaycareFull(d);
                    }
                    catch
                    {
                        Console.WriteLine(json);
                    }
                }
            }
            else
            {
                // Parse filters: topN=, wards=1,2, programs=Name1,Name2
                var topN = 50;
                var wardFilter = new HashSet<int>();
                var programFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string? nearValue = null;

                if (!string.IsNullOrEmpty(rest))
                {
                    var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var token in tokens)
                    {
                        var kv = token.Split('=', 2);
                        if (kv.Length != 2) continue;
                        var key = kv[0].ToLowerInvariant();
                        var value = kv[1];
                        if (key == "topn")
                        {
                            if (int.TryParse(value, out var n)) topN = n;
                        }
                        else if (key == "wards")
                        {
                            foreach (var w in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                if (int.TryParse(w, out var wi)) wardFilter.Add(wi);
                        }
                        else if (key == "programs")
                        {
                            foreach (var p in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                programFilter.Add(p);
                        }
                        else if (key == "near")
                        {
                            nearValue = value;
                        }
                    }
                }

                // Build arguments and forward all filters to the server
                var callArgs = new Dictionary<string, object?>();
                callArgs["topN"] = topN;
                callArgs["wards"] = wardFilter.Count > 0 ? wardFilter.ToArray() : null;
                callArgs["programs"] = programFilter.Count > 0 ? programFilter.ToArray() : null;
                callArgs["near"] = nearValue;
                // If near requested, request GPS info server-side
                callArgs["options"] = !string.IsNullOrEmpty(nearValue) ? (object)1 : 0;

                var result = await client.CallToolAsync("ListDaycares", callArgs, cancellationToken: CancellationToken.None);
                var json = ExtractJsonFromContent(result.Content);
                if (json is null)
                {
                    Console.WriteLine("No data returned");
                    continue;
                }

                // Try to deserialize DaycareSearchResponse (server now returns TopPrograms). Fall back to list of Daycare.
                List<TorontoDaycares.Models.Daycare> daycares;
                var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                jsonOptions.Converters.Add(new JsonStringEnumConverter());
                try
                {
                    var resp = System.Text.Json.JsonSerializer.Deserialize<TorontoDaycares.Models.DaycareSearchResponse>(json, jsonOptions);
                    if (resp is not null && resp.TopPrograms != null)
                    {
                        daycares = resp.TopPrograms.Select(tp => tp.Daycare).GroupBy(d => d.Id).Select(g => g.First()).ToList();
                    }
                    else
                    {
                        daycares = System.Text.Json.JsonSerializer.Deserialize<List<TorontoDaycares.Models.Daycare>>(json, jsonOptions) ?? new List<TorontoDaycares.Models.Daycare>();
                    }
                }
                catch
                {
                    daycares = System.Text.Json.JsonSerializer.Deserialize<List<TorontoDaycares.Models.Daycare>>(json, jsonOptions) ?? new List<TorontoDaycares.Models.Daycare>();
                }

                if (daycares is null || daycares.Count == 0)
                {
                    Console.WriteLine("No daycares available");
                    continue;
                }

                foreach (var d in daycares)
                    PrintDaycareSummary(d);
            }
        }
        else if (cmd == "wards")
        {
            var result = await client.CallToolAsync("ListWards", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None);
            if (!result.Content.Any())
            {
                Console.WriteLine("No wards returned");
            }
            else
            {
                foreach (var block in result.Content)
                {
                    var t = block.GetType();
                    var textProp = t.GetProperty("Text");
                    if (textProp != null)
                    {
                        Console.WriteLine(textProp.GetValue(block));
                        continue;
                    }
                    var valueProp = t.GetProperty("Value");
                    if (valueProp != null)
                    {
                        Console.WriteLine(valueProp.GetValue(block));
                        continue;
                    }
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(block));
                }
            }
        }
        else
        {
            Console.WriteLine("Unknown command");
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error: {e.Message}");
    }
}

Console.WriteLine("Exiting");

static string? ExtractJsonFromContent(IEnumerable<object> contentBlocks)
{
    foreach (var block in contentBlocks)
    {
        var t = block.GetType();
        var valueProp = t.GetProperty("Value");
        if (valueProp != null)
        {
            var val = valueProp.GetValue(block);
            if (val == null) continue;
            if (val is string s)
            {
                // Heuristic: if it looks like JSON, return
                var trimmed = s.TrimStart();
                if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
                    return s;
                // Otherwise return serialized form
                return JsonSerializer.Serialize(s);
            }

            try
            {
                return JsonSerializer.Serialize(val, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }

        var textProp = t.GetProperty("Text");
        if (textProp != null)
        {
            var s = textProp.GetValue(block) as string;
            if (!string.IsNullOrEmpty(s))
                return s;
        }
    }

    return null;
}

static void PrintDaycareSummary(TorontoDaycares.Models.Daycare d)
{
    Console.WriteLine($"{d.Id}: {d.Name} - {d.Address}{(string.IsNullOrWhiteSpace(d.Unit) ? string.Empty : ", " + d.Unit)} (Ward {d.WardNumber})");
    if (d.GpsCoordinates is not null)
        Console.WriteLine($"    GPS: {d.GpsCoordinates.Latitute}, {d.GpsCoordinates.Longitude}");
    Console.WriteLine($"    Programs: {string.Join("; ", d.Programs.Select(p => $"{p.ProgramType} (cap:{p.Capacity}, vac:{(p.Vacancy.HasValue ? (p.Vacancy.Value ? "yes" : "no") : "?")}, rating:{(p.Rating.HasValue ? p.Rating.ToString() : "n/a")})"))}");
}

static void PrintDaycareFull(TorontoDaycares.Models.Daycare d)
{
    Console.WriteLine("-".PadRight(80, '-'));
    PrintDaycareSummary(d);
    Console.WriteLine($"    URL: {d.Uri}");
    if (!string.IsNullOrWhiteSpace(d.NearestIntersection))
        Console.WriteLine($"    Nearest intersection: {d.NearestIntersection}");
}

static double GreatCircleDistance(Coordinates a, Coordinates b)
{
    const double EarthRadius = 6371; // km

    double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    var lat1 = DegreesToRadians(a.Latitute);
    var lon1 = DegreesToRadians(a.Longitude);
    var lat2 = DegreesToRadians(b.Latitute);
    var lon2 = DegreesToRadians(b.Longitude);

    var dlat = lat2 - lat1;
    var dlon = lon2 - lon1;

    var h = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) +
            Math.Cos(lat1) * Math.Cos(lat2) *
            Math.Sin(dlon / 2) * Math.Sin(dlon / 2);

    return 2 * EarthRadius * Math.Asin(Math.Sqrt(h));
}

static void PrintRawResult(CallToolResult result)
{
    foreach (var block in result.Content)
    {
        var t = block.GetType();
        var textProp = t.GetProperty("Text");
        if (textProp != null)
        {
            Console.WriteLine(textProp.GetValue(block));
            continue;
        }
        var valueProp = t.GetProperty("Value");
        if (valueProp != null)
        {
            Console.WriteLine(valueProp.GetValue(block));
            continue;
        }
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(block));
    }
}
