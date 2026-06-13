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

AIAgent agent = new ChatClientAgent(
    chatClient,
    instructions: "You are a friendly assistant. Keep your answers brief.");

// ---- Turn 1 in a session ----
AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine("Turn 1:");
Console.WriteLine(await agent.RunAsync("My name is Alice and I love hiking.", session));

Console.WriteLine("\nTurn 2:");
Console.WriteLine(await agent.RunAsync("I also live in Paris.", session));

// ---- SAVE the session (in a real app, this string goes to your SQL DB) ----
var serialized = agent.SerializeSessionAsync(session);
Console.WriteLine("\n--- Serialized session (this is what you'd store in SQL) ---");
Console.WriteLine(serialized);
Console.WriteLine("--- end ---\n");

// ---- Imagine: app shuts down, days pass, new request comes in ----
// You'd load 'serialized' back from the DB here.

// ---- RESTORE and continue — it still remembers ----
AgentSession resumed = await agent.DeserializeSessionAsync(serialized.Result);
Console.WriteLine("After restore:");
Console.WriteLine(await agent.RunAsync("What do you remember about me?", resumed));
Console.ReadLine();