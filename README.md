# MakingMcp

[English](README.md) | [中文](README.zh.md)

## Overview
MakingMcp provides a Model Context Protocol (MCP) server implementation that integrates file, web, shell, and delegated-agent tooling through dependency injection. The project offers two deployment modes:

- **Stdio Mode** (`src/MakingMcp`) - Traditional command-line MCP server using standard input/output
- **Web Service Mode** (`src/MakingMcp.Web`) - HTTP-based MCP server with Windows Service support

## Prerequisites
- .NET SDK 10.0 or later

## Getting Started

### Download
Download the pre-built binaries from [GitHub Releases](https://github.com/your-repo/MakingMcp/releases):
- **MakingMcp-win-x64.zip** - Stdio mode for Windows
- **MakingMcp-linux-x64.zip** - Stdio mode for Linux
- **MakingMcp-osx-x64.zip** - Stdio mode for macOS
- **MakingMcp-Web-win-x64.zip** - Web service mode for Windows
- **MakingMcp-Web-osx-x64.zip** - Web service mode for macOS

### Stdio Mode
1. Extract the downloaded package
2. Run the executable:
   - **Windows**: `MakingMcp.exe`
   - **Linux/macOS**: `./MakingMcp`

### Web Service Mode
1. Extract the downloaded package (MakingMcp-Web-*.zip)
2. Run as a regular web application:
   - **Windows**: `MakingMcp.Web.exe`
   - **macOS**: `./MakingMcp.Web`

   The MCP endpoint will be available at `http://localhost:6511/mcp` (or as configured in your environment).

3. **Install as Windows Service** (requires Administrator privileges):
   ```bash
   MakingMcp.Web.exe install
   ```
   The service will be automatically started after installation.

4. **Uninstall Windows Service** (requires Administrator privileges):
   ```bash
   MakingMcp.Web.exe uninstall
   ```

### Configuration
Both modes support configuration through environment variables:
- `OPENAI_API_KEY` - OpenAI API key for Task tool
- `OPENAI_ENDPOINT` - Custom OpenAI endpoint (optional)
- `TASK_MODEL` - Model name for Task tool (e.g., gpt-4)
- `EMBEDDING_MODEL` - Embedding model (default: text-embedding-3-small)
- `MAX_OUTPUT_TOKENS` - Maximum output tokens (default: 32000)
- `TAVILY_API_KEY` - Tavily API key for Web search tool

For Web Service mode, you can also use `appsettings.json` for configuration.

## Folder Structure
- `src/MakingMcp/` – Stdio mode application entry point
- `src/MakingMcp.Web/` – Web service mode application entry point
- `src/MakingMcp.Shared/` – Shared code (tools, options, models)
  - `Tools/` – MCP tools (file, web, bash, agent, task)
  - `Options/` – Configuration objects
  - `Model/` – Shared DTOs
- `bin/`, `obj/` – Build output and transient assets (not under version control)

## Contributing
Follow the repository guidelines in `AGENTS.md`, format code with `dotnet format`, and provide focused commits paired with relevant tests. Open a pull request with a clear summary, validation steps, and any supporting screenshots or logs when tool outputs change.


## WeChat
![84bab9b161235680f0a53fc5d4f5f3f2](https://github.com/user-attachments/assets/c16abfc9-d285-40be-b7c4-5ca6fc2cd9ab)

