using System;
using System.ClientModel;
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

// ---- Agent ----
AIAgent agent = new ChatClientAgent(
    chatClient,
    instructions: "You are a friendly assistant. Keep your answers brief.");

// ---- A SESSION carries memory across turns ----
AgentSession session = await agent.CreateSessionAsync();

// Turn 1 — tell it something
Console.WriteLine("Turn 1:");
Console.WriteLine(await agent.RunAsync("My name is Alice and I love hiking.", session));

// Turn 2 — it remembers, because we passed the SAME session
Console.WriteLine("\nTurn 2:");
Console.WriteLine(await agent.RunAsync("What do you remember about me?", session));

// Turn 3 — reasons using remembered info
Console.WriteLine("\nTurn 3:");
Console.WriteLine(await agent.RunAsync("Suggest a weekend activity for me.", session));