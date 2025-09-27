# 仓库指南

## 项目结构与模块组织
解决方案的核心位于 `src/AgentMcp`，其中 `Program.cs` 负责组装 Model Context Protocol 服务和依赖注入。`Tools/` 文件夹包含独立的 MCP 工具（`FileTool`、`WebTool`、`BashTool`、`AgentTool`），它们分别提供文件、网络和 Shell 能力；在扩展新端点时应基于这些类开展工作。配置对象位于 `Options/`，共享 DTO 则存放在 `Model/`。构建产物输出到 `bin/`，临时资源写入 `obj/`，请确保两者都不纳入版本控制。

## 构建、测试与开发命令
每个环境只需运行一次 `dotnet restore` 以拉取依赖。使用 `dotnet build src/AgentMcp/AgentMcp.csproj` 进行干净编译。`dotnet run --project src/AgentMcp/AgentMcp.csproj` 会通过标准输入输出启动 MCP 服务器，便于在本地 IDE 中集成。打包 NuGet 时执行 `dotnet pack src/AgentMcp/AgentMcp.csproj -c Release`，构建产物将生成在 `bin/Release` 目录。

## 编码风格与命名约定
目标框架为 `net8.0`；推荐使用现代 C# 特性（文件作用域命名空间、`await using`、模式匹配）。统一使用四个空格缩进，并保持文件为无 BOM 的 UTF-8 编码。类与公共成员使用 PascalCase 命名，本地变量采用 camelCase，异步方法以 `Async` 结尾。新增工具或选项时遵循现有的文件夹边界，并在提交前运行 `dotnet format`。

## 测试指南
目前尚未引入自动化测试；如需新增测试，请在同级的 `tests/` 目录下使用 xUnit。测试项目命名为 `<Feature>.Tests`，测试方法采用 `MethodName_State_ExpectedOutcome` 格式。从仓库根目录运行 `dotnet test` 以验证覆盖率，并确保新的代码改动配套有有意义的测试用例。

## 提交与拉取请求指南
提交信息使用祈使句（例如 `Add WebTool retry policy`），主题限制在 72 个字符以内。保持提交聚焦，将相关更改及其测试放在一起。拉取请求需要概括改动内容，列出验证步骤（命令、手动检查），并关联相关的跟踪问题。若修改了工具输出，请附上截图或日志。在请求评审前，务必确保项目能够构建且 MCP 服务器可以正常启动。

## Agent 专项提示
新增工具时，应通过 `ToolDescriptor` 暴露命令并确保参数校验。遵循既有的日志记录模式以维持一致的遥测数据。为新的能力编写 `README.md` 文档，帮助下游 MCP 客户了解可用工具及所需配置。
