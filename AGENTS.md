# Repository Guidelines

## Project Structure & Module Organization
The solution centers on `src/AgentMcp`, where `Program.cs` wires up Model Context Protocol services and dependency injection. The `Tools/` folder holds discrete MCP tools (`FileTool`, `WebTool`, `BashTool`, `AgentTool`) that expose file, web, and shell capabilities; extend these classes when adding new endpoints. Configuration objects live in `Options/` and shared DTOs in `Model/`. Build artifacts land in `bin/` and transient assets in `obj/`; keep both out of version control.

## Build, Test, and Development Commands
Run `dotnet restore` once per environment to pull dependencies. Use `dotnet build src/AgentMcp/AgentMcp.csproj` for a clean compile. `dotnet run --project src/AgentMcp/AgentMcp.csproj` launches the MCP server over stdio for local IDE integration. When packaging for NuGet, execute `dotnet pack src/AgentMcp/AgentMcp.csproj -c Release` to emit artifacts under `bin/Release`.

## Coding Style & Naming Conventions
Target framework is `net8.0`; prefer modern C# features (file-scoped namespaces, `await using`, pattern matching). Use four spaces for indentation and keep files in UTF-8 without BOM. Name classes and public members with PascalCase, locals with camelCase, and asynchronous methods with an `Async` suffix. Follow existing folder boundaries when introducing new tools or options, and run `dotnet format` before submitting changes.

## Testing Guidelines
Automated tests are not yet present; add them under a sibling `tests/` directory using xUnit. Name test projects `<Feature>.Tests` and individual test methods `MethodName_State_ExpectedOutcome`. Execute `dotnet test` from the repository root to validate coverage, and keep new code changes paired with meaningful test cases.

## Commit & Pull Request Guidelines
Write commit messages in the imperative mood (e.g., `Add WebTool retry policy`) and limit the subject to 72 characters. Provide focused commits that group related changes alongside their tests. Pull requests should summarize the change, outline validation steps (commands, manual checks), and link any tracking issues. Include screenshots or logs when altering tool outputs. Before requesting review, ensure the project builds and the MCP server starts cleanly.

## Agent-Specific Tips
When adding tools, expose commands through `ToolDescriptor` definitions and ensure argument validation. Respect existing logger patterns for consistent telemetry. Document new capabilities in `README.md` so downstream MCP clients understand available tools and required configuration.
