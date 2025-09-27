# Repository Guidelines

## Project Structure & Module Organization
The `src/MakingMcp` project hosts the MCP server. `Program.cs` wires dependency injection, reads `OpenAIOptions`, and registers tools. Tool implementations live under `Tools/` (e.g., `ReadTool.cs`, `BashTool.cs`, `TaskTool.cs`). DTOs reside in `Model/`, configuration types in `Options/`, and `.mcp/server.json` captures default transports. Build outputs (`bin/`, `obj/`) are generated per configuration and stay untracked.

## Build, Test, and Development Commands
- `dotnet restore`: install NuGet dependencies before first build.
- `dotnet build src/MakingMcp/MakingMcp.csproj`: compile the console host.
- `dotnet run --project src/MakingMcp/MakingMcp.csproj tools=Task,Web`: start the MCP server over stdio with selected tool sets.
- `dotnet pack src/MakingMcp/MakingMcp.csproj -c Release`: create NuGet packages in `bin/Release` when distributing binaries.

## Coding Style & Naming Conventions
Target `net8.0` and use nullable reference types. Prefer file-scoped namespaces, expression-bodied members when clear, and four-space indentation. Classes, records, and public members use PascalCase; locals and parameters use camelCase; async methods end with `Async`. Group new tools beneath `Tools/` and keep options and DTOs in their existing folders. Run `dotnet format` or the `Format Document` IDE command before committing.

## Testing Guidelines
Unit tests are not yet present; add xUnit projects under `tests/`. Name test assemblies `<Module>.Tests` and methods `MethodUnderTest_State_Expected`. Use fakes for external services (OpenAI, Tavily) and verify tool behavior. Run `dotnet test` from the repo root and ensure new tests cover error paths for agent interactions.

## Commit & Pull Request Guidelines
Write imperative commit subjects under 72 characters (e.g., `Add multi-edit range validation`). Keep commits scoped to a feature or fix plus relevant tests. Pull requests should describe the change, list validation commands, and link any issues. Include screenshots or logs when tool output or telemetry changes. Confirm `dotnet build` and `dotnet run` succeed locally before requesting review.

## Agent & Configuration Tips
Expose tools through `McpServerTool` factory methods and validate arguments before executing shell or network actions. Document new environment variables in `README.md`. Keep API keys (OpenAI, Tavily) outside source control and load them via `OpenAIOptions.Init`.
