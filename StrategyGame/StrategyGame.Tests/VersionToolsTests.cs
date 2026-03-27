using StrategyGame.McpServer.Tools;
using Xunit;

namespace StrategyGame.Tests;

/// <summary>
/// Tests for the version tool. These run against the actual git repo,
/// so they validate real output format rather than mocking git.
/// </summary>
public sealed class VersionToolsTests
{
    // ── BuildVersionString (unit-style, checks format) ────────────────────

    [Fact]
    public void BuildVersionString_ReturnsNonEmptyString()
    {
        var version = VersionTools.BuildVersionString();
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public void BuildVersionString_ContainsVersionTag()
    {
        // The repo has v0.1.0 tagged — version string should start with it (possibly with extra info)
        var version = VersionTools.BuildVersionString();
        Assert.StartsWith("v", version);
    }

    [Fact]
    public void BuildVersionString_ContainsV010Tag()
    {
        var version = VersionTools.BuildVersionString();
        Assert.Contains("v0.1.0", version);
    }

    // ── GetVersion (full tool output) ────────────────────────────────────────

    [Fact]
    public void GetVersion_OutputContainsVersionLine()
    {
        var output = VersionTools.GetVersion();
        Assert.Contains("Version:", output);
    }

    [Fact]
    public void GetVersion_OutputContainsRecentCommitsHeader()
    {
        var output = VersionTools.GetVersion();
        Assert.Contains("Recent commits", output);
    }

    [Fact]
    public void GetVersion_OutputContainsAtLeastOneCommit()
    {
        var output = VersionTools.GetVersion();
        // Should have commit lines after the header
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var commitLines = lines.Skip(2).Where(l => l.Trim().Length > 0).ToList();
        Assert.NotEmpty(commitLines);
    }

    [Fact]
    public void GetVersion_ShowsAtMostTwentyCommits()
    {
        var output = VersionTools.GetVersion();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Skip "Version: ..." and "Recent commits..." header lines
        var commitLines = lines.Skip(2).ToList();
        Assert.True(commitLines.Count <= 20, $"Expected at most 20 commit lines, got {commitLines.Count}");
    }

    [Fact]
    public void GetVersion_CommitLinesAreOneLine()
    {
        // Each commit entry should be exactly one line (not multi-line)
        var output = VersionTools.GetVersion();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var commitLines = lines.Skip(2).ToList();
        foreach (var line in commitLines)
            Assert.DoesNotContain('\r', line);
    }

    [Fact]
    public void GetVersion_KnownCommitAppearsInHistory()
    {
        // The v0.1.0 tag commit message is known — it must appear in recent history
        var output = VersionTools.GetVersion();
        Assert.Contains("add unit and integration tests", output);
    }

    // ── RunGit helper ─────────────────────────────────────────────────────

    [Fact]
    public void RunGit_InvalidCommand_ReturnsEmptyString()
    {
        var result = VersionTools.RunGit("this-command-does-not-exist-xyz");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void RunGit_LogOneline_ReturnsNonEmpty()
    {
        var result = VersionTools.RunGit("log --oneline -1");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
