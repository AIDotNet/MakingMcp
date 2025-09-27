# AgentMcp

[英文](README.md) | [中文](README.zh.md)

## 概述
AgentMcp 提供一个 Model Context Protocol（MCP）服务器，通过依赖注入整合文件、网络、Shell 以及代理委托等工具。核心实现位于 `src/AgentMcp`，其中 `Program.cs` 负责完成服务注册和工具暴露。

## 先决条件
- .NET SDK 8.0 或更高版本

## 快速开始
1. 在新环境中运行一次依赖恢复：
   ```bash
   dotnet restore
   ```
2. 编译项目：
   ```bash
   dotnet build src/AgentMcp/AgentMcp.csproj
   ```
3. 通过标准输入输出启动 MCP 服务器：
   ```bash
   dotnet run --project src/AgentMcp/AgentMcp.csproj
   ```
4. 如需打包 NuGet：
   ```bash
   dotnet pack src/AgentMcp/AgentMcp.csproj -c Release
   ```

## 目录结构
- `src/AgentMcp/` – 应用入口与服务装配
- `src/AgentMcp/Tools/` – MCP 工具（文件、网络、Bash、代理）
- `src/AgentMcp/Options/` – 配置对象
- `src/AgentMcp/Model/` – 共享 DTO
- `bin/`、`obj/` – 构建输出及临时资源（不纳入版本控制）

## 文档
- 开发与贡献指南（英文）：`AGENTS.md`
- 开发与贡献指南（中文）：`AGENTS.zh.md`

## 贡献指南
遵循 `AGENTS.md` 中的仓库规范，提交前运行 `dotnet format`，并确保提交集中且附带相关测试。创建拉取请求时，请提供清晰的改动摘要、验证步骤以及在工具输出变更时的截图或日志。
