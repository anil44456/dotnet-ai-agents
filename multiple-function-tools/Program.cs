using System;
using System.ClientModel;
using System.ComponentModel;
using OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// ---------- TOOL 1: weather ----------
[Description("Get the weather for a given location.")]
static string GetWeather(
    [Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15\u00b0C.";


// ---------- TOOL 2: time ----------
[Description("Get the current local time for a given city.")]
static string GetTimeInCity(
    [Description("The city name, e.g. 'Tokyo' or 'Hyderabad'.")] string city)
{
    var now = DateTime.UtcNow;
    return city.ToLower() switch
    {
        "hyderabad" or "mumbai" or "delhi" => now.AddHours(5.5).ToString("h:mm tt") + " IST",
        "tokyo" => now.AddHours(9).ToString("h:mm tt") + " JST",
        "london" => now.ToString("h:mm tt") + " GMT",
        "new york" => now.AddHours(-5).ToString("h:mm tt") + " EST",
        _ => $"Sorry, I don't have the timezone for {city}."
    };
}

// ---------- TOOL 3: currency converter ----------
[Description("Convert an amount from one currency to another using demo rates.")]
static string ConvertCurrency(
    [Description("The amount to convert.")] double amount,
    [Description("From currency code, e.g. 'USD'.")] string from,
    [Description("To currency code, e.g. 'INR'.")] string to)
{
    // demo rates relative to USD
    var rates = new System.Collections.Generic.Dictionary<string, double>
    {
        ["USD"] = 1.0,
        ["INR"] = 83.0,
        ["EUR"] = 0.92,
        ["GBP"] = 0.79,
        ["JPY"] = 156.0
    };
    from = from.ToUpper(); to = to.ToUpper();
    if (!rates.ContainsKey(from) || !rates.ContainsKey(to))
        return $"Sorry, I don't have rates for {from} or {to}.";
    double result = amount / rates[from] * rates[to];
    return $"{amount} {from} = {result:F2} {to} (demo rate)";
}

// ---------- Free GitHub Models client ----------
string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("Set GITHUB_TOKEN.");

IChatClient chatClient = new OpenAIClient(
        new ApiKeyCredential(token),
        new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") })
    .GetChatClient("openai/gpt-4o-mini")
    .AsIChatClient();

// ---------- Agent with ALL THREE tools ----------
AIAgent agent = new ChatClientAgent(
    chatClient,
    instructions: "You are a helpful assistant. Use tools when relevant.",
    tools:
    [
        AIFunctionFactory.Create(GetWeather),
        AIFunctionFactory.Create(GetTimeInCity),
        AIFunctionFactory.Create(ConvertCurrency)
    ]);

// ---------- Interactive loop ----------
Console.WriteLine("Ask me about weather, time, or currency. Type 'exit' to quit.\n");
while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input == "exit") break;
    Console.WriteLine("Agent: " + await agent.RunAsync(input) + "\n");
}