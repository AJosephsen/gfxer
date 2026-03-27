using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace StrategyGame.McpServer.Tools;

[McpServerToolType]
public static class VersionTools
{
    [McpServerTool(Name = "get_version")]
    [Description(
        "Returns the server version derived from git: latest tag, number of commits ahead of tag, " +
        "short commit hash if ahead of tag, and a count of uncommitted changes if the working tree is dirty. " +
        "Also lists the first line of the last 20 commit messages.")]
    public static string GetVersion()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Version: {BuildVersionString()}");
        sb.AppendLine();
        sb.AppendLine("Recent commits (last 20):");

        var log = RunGit("log --oneline -20").Trim();
        if (string.IsNullOrEmpty(log))
            sb.AppendLine("  (no commits found)");
        else
            foreach (var line in log.Split('\n'))
                sb.AppendLine($"  {line.Trim()}");

        return sb.ToString().TrimEnd();
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    internal static string BuildVersionString()
    {
        // git describe --tags --long  →  v0.1.0-3-gabc1234
        var describe = RunGit("describe --tags --long").Trim();

        string tag;
        string shortHash;
        int commitsSinceTag;

        if (string.IsNullOrEmpty(describe))
        {
            tag = "untagged";
            shortHash = RunGit("rev-parse --short HEAD").Trim();
            commitsSinceTag = 0;
        }
        else
        {
            // Format: {tag}-{n}-g{hash}  — tag itself may contain dashes, so parse from the right
            var lastDash = describe.LastIndexOf('-');
            var secondLastDash = describe.LastIndexOf('-', lastDash - 1);
            shortHash = describe[(lastDash + 2)..]; // skip the leading 'g'
            commitsSinceTag = int.Parse(describe[(secondLastDash + 1)..lastDash]);
            tag = describe[..secondLastDash];
        }

        var statusOutput = RunGit("status --porcelain").Trim();
        var dirtyCount = string.IsNullOrEmpty(statusOutput)
            ? 0
            : statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        var isDirty = dirtyCount > 0;

        return (commitsSinceTag, isDirty) switch
        {
            (0, false) => tag,
            (0, true)  => $"{tag} (dirty: {dirtyCount} uncommitted change{(dirtyCount == 1 ? "" : "s")})",
            (_, false) => $"{tag}+{commitsSinceTag} ({shortHash})",
            (_, true)  => $"{tag}+{commitsSinceTag} ({shortHash}, dirty: {dirtyCount} uncommitted change{(dirtyCount == 1 ? "" : "s")})",
        };
    }

    internal static string RunGit(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
