# MakingMcp

[英文](README.md) | [中文](README.zh.md)

## 概述
MakingMcp 提供 Model Context Protocol（MCP）服务器实现，通过依赖注入整合文件、网络、Shell 以及代理委托等工具。项目提供两种部署模式：

- **Stdio 模式** (`src/MakingMcp`) - 传统的命令行 MCP 服务器，使用标准输入输出
- **Web 服务模式** (`src/MakingMcp.Web`) - 基于 HTTP 的 MCP 服务器，支持 Windows 服务

## 先决条件
- .NET SDK 10.0 或更高版本

## 快速开始

### 下载
从 [GitHub Releases](https://github.com/your-repo/MakingMcp/releases) 下载预编译的二进制文件：
- **MakingMcp-win-x64.zip** - Windows 的 Stdio 模式
- **MakingMcp-linux-x64.zip** - Linux 的 Stdio 模式
- **MakingMcp-osx-x64.zip** - macOS 的 Stdio 模式
- **MakingMcp-Web-win-x64.zip** - Windows 的 Web 服务模式
- **MakingMcp-Web-osx-x64.zip** - macOS 的 Web 服务模式

### Stdio 模式
1. 解压下载的压缩包
2. 运行可执行文件：
   - **Windows**: `MakingMcp.exe`
   - **Linux/macOS**: `./MakingMcp`

### Web 服务模式
1. 解压下载的压缩包（MakingMcp-Web-*.zip）
2. 作为普通 Web 应用运行：
   - **Windows**: `MakingMcp.Web.exe`
   - **macOS**: `./MakingMcp.Web`

   MCP 端点将在 `http://localhost:6511/mcp` 可用（或根据您的环境配置）。

3. **安装为 Windows 服务**（需要管理员权限）：
   ```bash
   MakingMcp.Web.exe install
   ```
   服务将在安装后自动启动。

4. **卸载 Windows 服务**（需要管理员权限）：
   ```bash
   MakingMcp.Web.exe uninstall
   ```

### 配置
两种模式都支持通过环境变量进行配置：
- `OPENAI_API_KEY` - Task 工具所需的 OpenAI API 密钥
- `OPENAI_ENDPOINT` - 自定义 OpenAI 端点（可选）
- `TASK_MODEL` - Task 工具使用的模型名称（例如：gpt-4）
- `EMBEDDING_MODEL` - 嵌入模型（默认：text-embedding-3-small）
- `MAX_OUTPUT_TOKENS` - 最大输出令牌数（默认：32000）
- `TAVILY_API_KEY` - Web 搜索工具所需的 Tavily API 密钥

对于 Web 服务模式，您也可以使用 `appsettings.json` 进行配置。

## 目录结构
- `src/MakingMcp/` – Stdio 模式应用入口
- `src/MakingMcp.Web/` – Web 服务模式应用入口
- `src/MakingMcp.Shared/` – 共享代码（工具、配置、模型）
  - `Tools/` – MCP 工具（文件、网络、Bash、代理、任务）
  - `Options/` – 配置对象
  - `Model/` – 共享 DTO
- `bin/`、`obj/` – 构建输出及临时资源（不纳入版本控制）

## 贡献指南
遵循 `AGENTS.md` 中的仓库规范，提交前运行 `dotnet format`，并确保提交集中且附带相关测试。创建拉取请求时，请提供清晰的改动摘要、验证步骤以及在工具输出变更时的截图或日志。
