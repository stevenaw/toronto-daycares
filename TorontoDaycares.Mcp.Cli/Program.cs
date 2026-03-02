using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

Console.WriteLine(System.Environment.CurrentDirectory);

Console.WriteLine("Starting MCP client...");
var clientTransport = new StdioClientTransport(new()
{
    Name = "Minimal MCP Server",
    Command = "dotnet run",
    Arguments = ["--project", "../../../../TorontoDaycares.Mcp"],
});

var client = await McpClient.CreateAsync(clientTransport);

// Print the list of tools available from the server.
foreach (var tool in await client.ListToolsAsync())
{
    Console.WriteLine($"{tool.Name} ({tool.Description})");
}

// Conversational loop that can utilize the tools via prompts.
//List<ChatMessage> messages = [];
//while (true)
//{
//    Console.Write("Prompt: ");
//    messages.Add(new(ChatRole.User, Console.ReadLine()));

//    List<ChatResponseUpdate> updates = [];
//    await foreach (ChatResponseUpdate update in client
//        .
//        .GetStreamingResponseAsync(messages, new() { Tools = [.. tools] }))
//    {
//        Console.Write(update);
//        updates.Add(update);
//    }
//    Console.WriteLine();

//    messages.AddMessages(updates);
//}



// Execute a tool (this would normally be driven by LLM tool invocations).
var result = await client.CallToolAsync(
    "Echo2",
    new Dictionary<string, object?>() { ["message"] = "Hello MCP!" },
    cancellationToken: CancellationToken.None);

// echo always returns one and only one text content object
Console.WriteLine(result.Content.OfType<TextContentBlock>().First().Text);
Console.Read();
