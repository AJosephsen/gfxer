#!/usr/bin/env bash
set -e

cd "$(dirname "$0")"

echo "==> Building..."
dotnet build -c Release

echo "==> Killing MCP server (VS Code will respawn on next tool call)..."
pkill -f StrategyGame.McpServer && echo "    Killed." || echo "    Not running."

echo "==> Restarting WebViewer on :5050..."
pkill -f StrategyGame.WebViewer && sleep 0.5 || true
nohup dotnet run --project StrategyGame.WebViewer -c Release > /tmp/webviewer.log 2>&1 &
echo "    Started (pid $!). Logs: /tmp/webviewer.log"

echo "==> Done."
