using System;
using System.Linq;
using System.ClientModel;
using System.Collections.Generic;
using OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("Set GITHUB_TOKEN.");

var openAIClient = new OpenAIClient(
    new ApiKeyCredential(token),
    new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") });

// ---- Embedding client: turns text into vectors ----
IEmbeddingGenerator<string, Embedding<float>> embedder =
    openAIClient.GetEmbeddingClient("openai/text-embedding-3-small")
                .AsIEmbeddingGenerator();

// ---- Chat client + agent ----
IChatClient chatClient = openAIClient.GetChatClient("openai/gpt-4o-mini").AsIChatClient();
AIAgent agent = new ChatClientAgent(chatClient,
    instructions: "Answer ONLY using the provided context. If it's not in the context, say you don't know.");

// ====== 1. OUR "DOCUMENTS" (chunks) ======
string[] documents =
{
    "Our refund policy allows returns within 30 days of purchase for a full refund.",
    "Shipping is free for orders over 50 dollars. Otherwise it costs 5 dollars.",
    "Customer support is available Monday to Friday, 9 AM to 6 PM IST.",
    "Premium members get early access to sales and a dedicated support line.",
    "Products damaged in shipping can be replaced free within 7 days of delivery."
};

// ====== 2. EMBED all documents (once) ======
Console.WriteLine("Embedding documents...");
var docEmbeddings = new List<(string text, float[] vector)>();
foreach (var doc in documents)
{
    var emb = await embedder.GenerateAsync(doc);
    docEmbeddings.Add((doc, emb.Vector.ToArray()));
}

// helper: cosine similarity (how close two vectors are)
static float Cosine(float[] a, float[] b)
{
    float dot = 0, ma = 0, mb = 0;
    for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; ma += a[i] * a[i]; mb += b[i] * b[i]; }
    return dot / ((float)Math.Sqrt(ma) * (float)Math.Sqrt(mb));
}

// ====== 3. ASK QUESTIONS ======
string[] questions =
{
    "How long do I have to return something?",
    "Is delivery free?",
    "When can I reach support?"
};

foreach (var question in questions)
{
    // embed the question
    var qEmb = (await embedder.GenerateAsync(question)).Vector.ToArray();

    // RETRIEVE: find the 2 most similar document chunks
    var topChunks = docEmbeddings
        .Select(d => (d.text, score: Cosine(qEmb, d.vector)))
        .OrderByDescending(x => x.score)
        .Take(2)
        .Select(x => x.text);

    string context = string.Join("\n", topChunks);

    // GENERATE: answer using only those chunks
    string prompt = $"Context:\n{context}\n\nQuestion: {question}";
    Console.WriteLine($"\nQ: {question}");
    Console.WriteLine($"A: {await agent.RunAsync(prompt)}");
}