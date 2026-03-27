using System.Diagnostics;

namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Helpers for invoking git commands.
/// </summary>
public static class GitHelpers
{
    /// <summary>
    /// Runs `git show ref:path` and returns the file content as a string.
    /// </summary>
    public static async Task<string> ShowFileAsync(string repoPath, string gitRef, string filePath)
    {
        var result = await RunGitAsync(repoPath, ["show", $"{gitRef}:{filePath}"]);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git show {gitRef}:{filePath} failed (exit {result.ExitCode}): {result.Error}");
        }

        return result.Output;
    }

    /// <summary>
    /// Runs a git command and captures stdout/stderr.
    /// </summary>
    public static async Task<GitResult> RunGitAsync(string workingDirectory, string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new GitResult(
            process.ExitCode,
            await outputTask,
            await errorTask
        );
    }
}

/// <summary>
/// Result of a git command execution.
/// </summary>
public record GitResult(int ExitCode, string Output, string Error);
