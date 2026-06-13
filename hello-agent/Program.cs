using System;
using System.ClientModel;
using OpenAI;
using Microsoft.Extensions.AI;

string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("Set GITHUB_TOKEN first.");

// Point an OpenAI-style client at the FREE GitHub Models endpoint
var openAIClient = new OpenAIClient(
    new ApiKeyCredential(token),
    new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") });

// Wrap it in IChatClient (the abstraction MAF builds on)
IChatClient chat = openAIClient
    .GetChatClient("openai/gpt-4o-mini")
    .AsIChatClient();

while (true)
{
    Console.Write("\nYou: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input == "exit") break;
    var reply = await chat.GetResponseAsync(input);
    Console.WriteLine("AI: " + reply.Text);
}
Console.ReadLine();