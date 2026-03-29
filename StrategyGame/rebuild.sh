#!/usr/bin/env bash
set -e

cd "$(dirname "$0")"

echo "==> Generating changelog.json..."
git log --format='%h	%s	%ad' --date=short -40 | python3 -c "
import sys, json
entries = []
for line in sys.stdin:
    parts = line.rstrip('\n').split('\t', 2)
    if len(parts) == 3:
        entries.append({'hash': parts[0], 'message': parts[1], 'date': parts[2]})
print(json.dumps(entries, indent=2))
" > StrategyGame.WebViewer/changelog.json
echo "    Written $(python3 -c "import json; print(len(json.load(open('StrategyGame.WebViewer/changelog.json'))))" ) entries."

echo "==> Building..."
dotnet build -c Release

echo "==> Killing MCP server (VS Code will respawn on next tool call)..."
pkill -f StrategyGame.McpServer && echo "    Killed." || echo "    Not running."

echo "==> Restarting WebViewer on :5050..."
pkill -f StrategyGame.WebViewer && sleep 0.5 || true
nohup dotnet run --project StrategyGame.WebViewer -c Release > /tmp/webviewer.log 2>&1 &
echo "    Started (pid $!). Logs: /tmp/webviewer.log"

echo "==> Done."
