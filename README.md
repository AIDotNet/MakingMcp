# AgentMcp

[English](README.md) | [中文](README.zh.md)

## Overview
AgentMcp hosts a Model Context Protocol (MCP) server that wires together file, web, shell, and delegated-agent tooling through dependency injection. The core implementation lives in `src/AgentMcp`, where `Program.cs` configures service registrations and tool exposure.

## Prerequisites
- .NET SDK 8.0 or later

## Getting Started
1. Restore dependencies (run once per environment):
   ```bash
   dotnet restore
   ```
2. Build the project:
   ```bash
   dotnet build src/AgentMcp/AgentMcp.csproj
   ```
3. Launch the MCP server over stdio:
   ```bash
   dotnet run --project src/AgentMcp/AgentMcp.csproj
   ```
4. Package for NuGet when required:
   ```bash
   dotnet pack src/AgentMcp/AgentMcp.csproj -c Release
   ```

## Folder Structure
- `src/AgentMcp/` – Application entry point and service wiring
- `src/AgentMcp/Tools/` – MCP tools (file, web, bash, agent)
- `src/AgentMcp/Options/` – Configuration objects
- `src/AgentMcp/Model/` – Shared DTOs
- `bin/`, `obj/` – Build output and transient assets (not under version control)

## Documentation
- Development and contribution guidelines (English): `AGENTS.md`
- Development and contribution guidelines (Chinese): `AGENTS.zh.md`

## Contributing
Follow the repository guidelines in `AGENTS.md`, format code with `dotnet format`, and provide focused commits paired with relevant tests. Open a pull request with a clear summary, validation steps, and any supporting screenshots or logs when tool outputs change.
