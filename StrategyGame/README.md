# StrategyGame

A .NET 10 turn-based strategy board game exposed as an MCP server.

## Requirements

- .NET 10 SDK
- VS Code with GitHub Copilot (for MCP tool integration)

## Build

```bash
cd StrategyGame
dotnet build -c Release
```

## Run tests

```bash
dotnet test -c Release
```

## MCP Server

The server is configured in `.vscode/mcp.json` and starts automatically when VS Code connects to it. It uses `--no-build`, so **you must build first** before the server starts.

### Restarting after a rebuild

VS Code keeps the MCP server process running even after you rebuild. To pick up a new binary:

1. Find and kill the old process:
   ```bash
   pkill -f StrategyGame.McpServer
   ```
2. VS Code will automatically respawn it on the next tool call.

Alternatively, use the **MCP: Restart Server** command in the VS Code command palette (`Ctrl+Shift+P` → `MCP: Restart Server` → `strategy-game`).
