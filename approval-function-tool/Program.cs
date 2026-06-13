using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.ComponentModel;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

// ---- A "dangerous" tool we want to gate behind approval ----
[Description("Delete a customer record by id. This is destructive.")]
static string DeleteCustomer([Description("The customer id to delete.")] string id)
    => $"Customer {id} has been deleted.";

string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("Set GITHUB_TOKEN.");

IChatClient chatClient = new OpenAIClient(
        new ApiKeyCredential(token),
        new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") })
    .GetChatClient("openai/gpt-4o-mini")
    .AsIChatClient();

// ---- Wrap the function so it REQUIRES approval ----
AIFunction deleteFn = AIFunctionFactory.Create(DeleteCustomer);
AIFunction approvalRequired = new ApprovalRequiredAIFunction(deleteFn);

AIAgent agent = new ChatClientAgent(
    chatClient,
    instructions: "You are a helpful admin assistant.",
    tools: [approvalRequired]);

// ---- Run; the agent will PAUSE and ask for approval ----
var session = await agent.CreateSessionAsync();
var response = await agent.RunAsync("Please delete customer C-123.", session);


// TODO: Human-in-the-loop approval — the response-handling API
// (FunctionApprovalRequestContent / CreateResponse) isn't available
// in this preview package version. Revisit when MAF approval stabilizes.
// Concept understood: dangerous tools pause for human y/n before running.


//// ---- Check if it's asking for approval instead of acting ----
//var functionApprovalRequests = response.Messages
//    .SelectMany(x => x.Contents)
//    .OfType<FunctionApprovalRequestContent>()
//    .ToList();

//if (approvalRequests.Count == 0)
//{
//    // No approval needed — just print the answer
//    Console.WriteLine(response);
//}
//else
//{
//    var req = approvalRequests.First();
//   // Console.WriteLine($"\n⚠ The agent wants to call: {req.FunctionCall.Name}");
//    //Console.WriteLine($"   Arguments: {string.Join(", ", req.FunctionCall.Arguments)}");
//    Console.Write("\nApprove? (y/n): ");
//    bool approved = Console.ReadLine()?.Trim().ToLower() == "y";

//    //// ---- Send the decision back into the SAME thread ----
//    //var approvalMessage = new ChatMessage(ChatRole.User, [req.CreateApprovalResponse(approved)]);
//    //var finalResponse = await agent.RunAsync(approvalMessage, session);
//    //Console.WriteLine("\nAgent: " + finalResponse);
//}