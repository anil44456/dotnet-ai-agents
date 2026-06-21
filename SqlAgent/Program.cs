// SQL Agent — an AI agent that converts natural-language questions into SQL Server
// queries, runs them read-only, and explains the results. Built with C# and the
// Microsoft Agent Framework.

using System.ComponentModel;
using System.ClientModel;
using Microsoft.Data.SqlClient;
using OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// ⚠️ YOUR connection string
const string ConnStr =
    "Server=localhost;Database=AgentSqlDemo;Trusted_Connection=True;TrustServerCertificate=True;";

// ============================================================
//  TOOL 1: GetSchema — discovers tables & columns
// ============================================================
[Description("Gets the database schema: all tables and their columns. Call this first to learn the structure before writing queries.")]
static string GetSchema()
{
    try
    {
        using var conn = new SqlConnection(ConnStr);
        conn.Open();
        var sb = new System.Text.StringBuilder();

        // ---- 1. Tables & columns ----
        var colSql = @"
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            ORDER BY TABLE_NAME, ORDINAL_POSITION";
        using (var cmd = new SqlCommand(colSql, conn))
        using (var reader = cmd.ExecuteReader())
        {
            string currentTable = "";
            while (reader.Read())
            {
                string table = reader["TABLE_NAME"].ToString()!;
                if (table != currentTable)
                {
                    sb.AppendLine($"\nTable: {table}");
                    currentTable = table;
                }
                sb.AppendLine($"  - {reader["COLUMN_NAME"]} ({reader["DATA_TYPE"]})");
            }
        }

        // ---- 2. Primary keys ----
        sb.AppendLine("\nPrimary Keys:");
        var pkSql = @"
            SELECT tc.TABLE_NAME, kcu.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
              ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'";
        using (var cmd = new SqlCommand(pkSql, conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                sb.AppendLine($"  - {reader["TABLE_NAME"]}.{reader["COLUMN_NAME"]}");
        }

        // ---- 3. Foreign keys (relationships) ----
        sb.AppendLine("\nForeign Keys (relationships):");
        var fkSql = @"
            SELECT 
                fk.name AS FK_Name,
                tp.name AS ParentTable,
                cp.name AS ParentColumn,
                tr.name AS ReferencedTable,
                cr.name AS ReferencedColumn
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
            JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
            JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
            JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id";
        using (var cmd = new SqlCommand(fkSql, conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                sb.AppendLine($"  - {reader["ParentTable"]}.{reader["ParentColumn"]} -> {reader["ReferencedTable"]}.{reader["ReferencedColumn"]}");
        }

        return sb.ToString();
    }
    catch (Exception ex) { return $"Schema error: {ex.Message}"; }
}

// ============================================================
//  TOOL 2: RunQuery — executes a SELECT and returns results
// ============================================================
[Description("Runs a SQL SELECT query against the database and returns the results as text.")]
static string RunQuery(
    [Description("A valid SQL Server SELECT query.")] string sql)
{
    // ---- SAFETY: only allow SELECT queries ----
    var trimmed = sql.TrimStart().ToUpperInvariant();
    if (!trimmed.StartsWith("SELECT") && !trimmed.StartsWith("WITH"))
        return "Blocked: only SELECT queries are allowed.";

    // block dangerous keywords even if smuggled in
    string[] banned = { "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE", "EXEC", "MERGE", "CREATE" };
    foreach (var word in banned)
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, $@"\b{word}\b"))
            return $"Blocked: '{word}' is not allowed. This agent is read-only.";

    try
    {
        using var conn = new SqlConnection(ConnStr);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < reader.FieldCount; i++)
            sb.Append(reader.GetName(i)).Append('\t');
        sb.AppendLine();
        while (reader.Read())
        {
            for (int i = 0; i < reader.FieldCount; i++)
                sb.Append(reader[i]?.ToString()).Append('\t');
            sb.AppendLine();
        }
        return sb.ToString();
    }
    catch (Exception ex) { return $"SQL error: {ex.Message}"; }
}

// holder so static tools can see the connection string

// ============================================================
//  THE AGENT
// ============================================================
string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("Set GITHUB_TOKEN.");

IChatClient chatClient = new OpenAIClient(
        new ApiKeyCredential(token),
        new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") })
    .GetChatClient("openai/gpt-4o-mini")
    .AsIChatClient();

AIAgent agent = new ChatClientAgent(
    chatClient,
    instructions: @"You are a SQL assistant for SQL Server.
First call the GetSchema tool to learn the available tables and columns.
Then write a SQL SELECT query to answer the user's question, call RunQuery to run it,
and explain the results in plain English. Only use SELECT queries.",
    tools:
    [
        AIFunctionFactory.Create(GetSchema),
        AIFunctionFactory.Create(RunQuery)
    ]);

// ============================================================
//  INTERACTIVE LOOP
// ============================================================
Console.WriteLine("SQL Agent ready. Ask about your data (type 'exit' to quit).\n");
while (true)
{
    Console.Write("You: ");
    var q = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(q) || q == "exit") break;
    Console.WriteLine("Agent: " + await agent.RunAsync(q) + "\n");
}