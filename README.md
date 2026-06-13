# .NET AI Agents

Learning to build AI agents in C# using the Microsoft Agent Framework
ecosystem, starting from fundamentals. Each folder is one step.

## 01 · Hello Agent (this project)

A C# console app that calls an LLM and prints the reply — the
foundation every agent is built on.

**Stack:** C# · .NET 10 · Microsoft.Extensions.AI · GitHub Models (free)
**Model:** gpt-4o-mini
**Concept:** Chat — a direct `IChatClient` call (no agent wrapper yet)

### How it works
1. Reads a GitHub token from the `GITHUB_TOKEN` environment variable
2. Points an `OpenAIClient` at the GitHub Models endpoint
3. Wraps it as an `IChatClient` (the abstraction MAF builds on)
4. Sends a prompt and prints the response

### Run it
