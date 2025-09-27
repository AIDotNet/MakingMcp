# AgentMcp MCP 服务端

本项目演示如何使用 C# 构建并发布一个基于 Model Context Protocol（MCP）的本地工具服务端。项目已经集成了以下能力：

- **文件工具 (FileTool)**：支持阅读、写入、单次与批量字符串替换、文件模式匹配、ripgrep 搜索等操作。
- **Web 工具 (WebTool)**：通过 Tavily API 进行网页抓取与搜索，统一返回 JSON 结构化结果。
- **Shell 工具 (BashTool)**：运行 bash 命令，支持后台会话、日志查询与终止。

## 环境准备

1. 安装 [.NET SDK 10.0](https://dotnet.microsoft.com/zh-cn/download) 或更高版本（项目 `TargetFramework` 为 `net10.0`）。
2. 如需使用 Web 工具，请在环境变量中设置 `TAVILY_API_KEY`。
3. 推荐在 Windows 使用 PowerShell、在 macOS/Linux 使用终端执行以下命令。

## 本地运行

```bash
dotnet build

dotnet run --project AgentMcp
```

默认会以 stdio 模式启动 MCP 服务器，可与支持 MCP 的客户端（如 VS Code、JetBrains Rider 等）进行连接调试。

## 配置到 IDE 中

在 IDE 中创建 MCP 服务器配置（示例为 VS Code）：

```json
{
  "mcpServers": {
    "AgentMcp": {
      "type": "stdio",
      "command": "dnx",
      "args": [
        "MakingMcp",
        "--yes"
      ],
      "env": {
        "TASK_MODEL": "gpt-4.1",
        "OPENAI_ENDPOINT": "https://api.token-ai.cn/v1",
        "API_KEY": "你的Key"
      }
    }
  }
}
```

## 主要工具简介

### FileTool

- `Read` / `Write`：读取或写入文件内容。
- `Edit` / `MultiEdit`：根据上下文精确替换文本，支持多段批量替换。
- `Glob`：使用类 glob 模式列出文件。
- `Grep`：封装 ripgrep 提供强大的代码搜索。

### WebTool（依赖 Tavily）

- `WebFetch`：抓取网页内容，自动清洗并生成摘要，JSON 格式返回。
- `WebSearch`：调用 Tavily 搜索并返回结构化结果与 AI 答案。

### BashTool

- `RunBashCommand`：支持同步执行或后台运行 bash 命令，内建超时控制与日志收集。
- `BashOutput`：查看后台命令最新输出，可按正则过滤。
- `KillBash`：终止后台执行的命令会话。

## 打包发布到 NuGet（可选）

```bash
dotnet pack -c Release
```

生成的 `.nupkg` 文件位于 `bin/Release` 目录，可自行上传到 NuGet。

## 反馈与贡献

欢迎提交 Issue 或 Pull Request 来改进此项目。如果你对 MCP 生态有想法，也可以在官方文档与社区中进行交流。祝开发顺利！