using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.ComponentModel;

Console.WriteLine("Hello, World!");


[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
{ 
    return $"The weather in {location} is cloudy with a high of 15\u00b0C.";
}

string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("Set GITHUB_TOKEN.");

IChatClient chatClient = new OpenAIClient(
        new ApiKeyCredential(token),
        new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") })
    .GetChatClient("openai/gpt-4o-mini")
    .AsIChatClient();

// Agent WITH the tool attached 
AIAgent agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
    ChatOptions = new ChatOptions
    {
        Instructions = "You are a helpful assistant.",
        Tools = [AIFunctionFactory.Create(GetWeather)]
    }
});

// ---- Run it — the agent calls GetWeather automatically ----
Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?"));

//while (true)
//{
//    Console.Write("\nYou: ");
//    var input = Console.ReadLine();
//    if (string.IsNullOrWhiteSpace(input) || input == "exit") break;
//    Console.WriteLine(await agent.RunAsync(input));
//}
Console.ReadLine();