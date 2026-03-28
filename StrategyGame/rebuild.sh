#!/usr/bin/env bash
set -e

cd "$(dirname "$0")"

echo "==> Building..."
dotnet build -c Release

echo "==> Killing MCP server (VS Code will respawn on next tool call)..."
pkill -f StrategyGame.McpServer && echo "    Killed." || echo "    Not running."

echo "==> Done."
