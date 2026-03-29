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

echo "==> Generating version.txt..."
python3 - <<'EOF'
import subprocess, sys

def git(args):
    r = subprocess.run(['git'] + args, capture_output=True, text=True)
    return r.stdout.strip() if r.returncode == 0 else ''

describe = git(['describe', '--tags', '--long'])
if not describe:
    tag = 'untagged'
    short_hash = git(['rev-parse', '--short', 'HEAD'])
    commits_since = 0
else:
    last  = describe.rfind('-')
    second = describe.rfind('-', 0, last)
    short_hash = describe[last+2:]
    commits_since = int(describe[second+1:last])
    tag = describe[:second]

dirty = git(['status', '--porcelain'])
dirty_count = len([l for l in dirty.splitlines() if l.strip()]) if dirty else 0
is_dirty = dirty_count > 0

if commits_since == 0 and not is_dirty:
    version = tag
elif commits_since == 0 and is_dirty:
    version = f"{tag} (dirty: {dirty_count} uncommitted change{'s' if dirty_count != 1 else ''})"
elif not is_dirty:
    version = f"{tag}+{commits_since} ({short_hash})"
else:
    version = f"{tag}+{commits_since} ({short_hash}, dirty: {dirty_count} uncommitted change{'s' if dirty_count != 1 else ''})"

with open('StrategyGame.WebViewer/version.txt', 'w') as f:
    f.write(version)
print(f'    Version: {version}')
EOF

echo "==> Building..."
dotnet build -c Release

echo "==> Killing MCP server (VS Code will respawn on next tool call)..."
pkill -f StrategyGame.McpServer && echo "    Killed." || echo "    Not running."

echo "==> Restarting WebViewer on :5050..."
pkill -f StrategyGame.WebViewer && sleep 0.5 || true
# Ensure version.txt lands in the output dir (MSBuild only copies it after a full restore cycle)
cp StrategyGame.WebViewer/version.txt StrategyGame.WebViewer/bin/Release/net10.0/version.txt
nohup dotnet run --project StrategyGame.WebViewer -c Release > /tmp/webviewer.log 2>&1 &
echo "    Started (pid $!). Logs: /tmp/webviewer.log"

echo "==> Done."
