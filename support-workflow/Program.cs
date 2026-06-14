using System;
using System.Linq;
using System.ClientModel;
using System.ComponentModel;
using System.Collections.Generic;
using OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;


string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("Set GITHUB_TOKEN.");

var openAIClient = new OpenAIClient(
    new ApiKeyCredential(token),
    new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") });

IChatClient chatClient = openAIClient.GetChatClient("openai/gpt-4o-mini").AsIChatClient();
var embedder = openAIClient.GetEmbeddingClient("openai/text-embedding-3-small").AsIEmbeddingGenerator();

// ===== knowledge base for RAG (step 3) =====
string[] kb =
{
    "Billing: Refunds are processed within 30 days of purchase. Duplicate charges are reversed within 5 business days.",
    "Technical: For login issues, reset your password via the 'Forgot Password' link. Clear cache if the app won't load.",
    "Shipping: Free over $50. Damaged items replaced free within 7 days.",
    "Account: To close an account, contact support; data is deleted within 14 days."
};
// pre-embed the KB
var kbVectors = new List<(string text, float[] vec)>();
foreach (var doc in kb)
    kbVectors.Add((doc, (await embedder.GenerateAsync(doc)).Vector.ToArray()));

static float Cosine(float[] a, float[] b)
{
    float dot = 0, ma = 0, mb = 0;
    for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; ma += a[i] * a[i]; mb += b[i] * b[i]; }
    return dot / ((float)Math.Sqrt(ma) * (float)Math.Sqrt(mb));
}

// agents (each a "step")
AIAgent classifier = new ChatClientAgent(chatClient,
    instructions: "You classify support tickets.");
AIAgent answerer = new ChatClientAgent(chatClient,
    instructions: "Answer the customer using ONLY the provided context. Be concise and polite.");

// ===== THE WORKFLOW =====
async Task HandleTicket(string ticket)
{
    Console.WriteLine($"\n=== TICKET: {ticket} ===");

    // ---- STEP 1: CLASSIFY (structured output) ----
    var c = (await classifier.RunAsync<TicketClass>(ticket)).Result;
    Console.WriteLine($"[classified] {c.Category} | {c.Priority} | upset={c.IsUpset}");

    // ---- STEP 2: BRANCH on category ----
    if (c.Priority is "Urgent" && c.IsUpset)
        Console.WriteLine(">> ESCALATE: notify a senior agent immediately.");

    switch (c.Category)
    {
        case "Billing":
        case "Technical":
            // ---- STEP 3: ANSWER via RAG ----
            var qVec = (await embedder.GenerateAsync(ticket)).Vector.ToArray();
            var top = kbVectors
                .Select(d => (d.text, score: Cosine(qVec, d.vec)))
                .OrderByDescending(x => x.score)
                .Take(2).Select(x => x.text);
            string context = string.Join("\n", top);
            string answer = (await answerer.RunAsync(
                $"Context:\n{context}\n\nCustomer: {ticket}")).ToString();
            Console.WriteLine($"[reply] {answer}");
            break;

        default:
            Console.WriteLine("[reply] Routed to a human agent (no automated handler).");
            break;
    }
}

// ===== run a few tickets =====
await HandleTicket("I was charged twice for my subscription this month!");
await HandleTicket("I can't log into the app, it just shows a blank screen.");
await HandleTicket("Do you ship to Antarctica?");


// ===== structured type for classification =====
class TicketClass
{
    [Description("Category: Billing, Technical, or Other")]
    public string Category { get; set; } = "";
    [Description("Priority: Low, Medium, High, Urgent")]
    public string Priority { get; set; } = "";
    [Description("True if customer sounds upset")]
    public bool IsUpset { get; set; }
}