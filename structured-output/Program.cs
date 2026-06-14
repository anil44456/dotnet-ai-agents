using System;
using System.ClientModel;
using System.ComponentModel;
using OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;



// ---- Free GitHub Models client ----
string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("Set GITHUB_TOKEN.");

IChatClient chatClient = new OpenAIClient(
        new ApiKeyCredential(token),
        new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") })
    .GetChatClient("openai/gpt-4o-mini")
    .AsIChatClient();

AIAgent agent = new ChatClientAgent(
    chatClient,
    instructions: "You analyze customer support tickets.");

// ---- Ask for a TYPED result instead of text ----
string ticket = "I've been charged twice for my subscription this month and " +
                "nobody has replied to my emails for THREE days. This is unacceptable!";

var result = await agent.RunAsync<TicketAnalysis>(ticket);
TicketAnalysis analysis = result.Result;

// ---- Now use it as real C# data ----
Console.WriteLine($"Category : {analysis.Category}");
Console.WriteLine($"Priority : {analysis.Priority}");
Console.WriteLine($"Summary  : {analysis.Summary}");
Console.WriteLine($"Upset?   : {analysis.IsUpset}");

// You can branch on it like normal code:
if (analysis.IsUpset && analysis.Priority is "High" or "Urgent")
    Console.WriteLine("\n>> Routing to a senior agent immediately.");

// ---- The C# type we want the agent to FILL ----
class TicketAnalysis
{
    [Description("Category: Billing, Technical, Account, or Other")]
    public string Category { get; set; } = "";

    [Description("Priority: Low, Medium, High, or Urgent")]
    public string Priority { get; set; } = "";

    [Description("A one-sentence summary of the issue")]
    public string Summary { get; set; } = "";

    [Description("True if the customer sounds angry or frustrated")]
    public bool IsUpset { get; set; }
}