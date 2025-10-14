using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Tools;

public class BashTool
{
    private const int DefaultTimeoutMs = 120_000;
    private const int MaxBufferedLines = 10_000;

    public static readonly ConcurrentDictionary<string, BashSession> Sessions =
        new(StringComparer.OrdinalIgnoreCase);

    [McpServerTool(Name = "Bash"), KernelFunction("Bash"), Description(
         """
         Executes a given bash command in a persistent shell session with optional timeout, ensuring proper handling and security measures.
         Before executing the command, please follow these steps:

         1. Directory Verification:
            - If the command will create new directories or files, first use `ls` to verify the parent directory exists and is the correct location
            - For example, before running "mkdir foo/bar", first use `ls foo` to check that "foo" exists and is the intended parent directory

         2. Command Execution:
            - Always quote file paths that contain spaces with double quotes (e.g., cd "path with spaces/file.txt")
            - Examples of proper quoting:
              - cd "/Users/name/My Documents" (correct)
              - cd /Users/name/My Documents (incorrect - will fail)
              - python "/path/with spaces/script.py" (correct)
              - python /path/with spaces/script.py (incorrect - will fail)
            - After ensuring proper quoting, execute the command.
            - Capture the output of the command.

         Usage notes:
           - The command argument is required.
           - You can specify an optional timeout in milliseconds (up to 600000ms / 10 minutes). If not specified, commands will timeout after 120000ms (2 minutes).
           - It is very helpful if you write a clear, concise description of what this command does in 5-10 words.
           - If the output exceeds 30000 characters, output will be truncated before being returned to you.
           - You can use the `run_in_background` parameter to run the command in the background, which allows you to continue working while the command runs. You can monitor the output using the Bash tool as it becomes available. Never use `run_in_background` to run 'sleep' as it will return immediately. You do not need to use '&' at the end of the command when using this parameter.
           - VERY IMPORTANT: You MUST avoid using search commands like `find` and `grep`. Instead use Grep, Glob, or Task to search. You MUST avoid read tools like `cat`, `head`, and `tail`, and use Read to read files.
          - If you _still_ need to run `grep`, STOP. ALWAYS USE ripgrep at `rg` first, which all Claude Code users have pre-installed.
           - When issuing multiple commands, use the ';' or '&&' operator to separate them. DO NOT use newlines (newlines are ok in quoted strings).
           - Try to maintain your current working directory throughout the session by using absolute paths and avoiding usage of `cd`. You may use `cd` if the User explicitly requests it.
             <good-example>
             pytest /foo/bar/tests
             </good-example>
             <bad-example>
             cd /foo/bar && pytest tests
             </bad-example>

         # Committing changes with git

         When the user asks you to create a new git commit, follow these steps carefully:

         1. You have the capability to call multiple tools in a single response. When multiple independent pieces of information are requested, batch your tool calls together for optimal performance. ALWAYS run the following bash commands in parallel, each using the Bash tool:
           - Run a git status command to see all untracked files.
           - Run a git diff command to see both staged and unstaged changes that will be committed.
           - Run a git log command to see recent commit messages, so that you can follow this repository's commit message style.
         2. Analyze all staged changes (both previously staged and newly added) and draft a commit message:
           - Summarize the nature of the changes (eg. new feature, enhancement to an existing feature, bug fix, refactoring, test, docs, etc.). Ensure the message accurately reflects the changes and their purpose (i.e. "add" means a wholly new feature, "update" means an enhancement to an existing feature, "fix" means a bug fix, etc.).
           - Check for any sensitive information that shouldn't be committed
           - Draft a concise (1-2 sentences) commit message that focuses on the "why" rather than the "what"
           - Ensure it accurately reflects the changes and their purpose
         3. You have the capability to call multiple tools in a single response. When multiple independent pieces of information are requested, batch your tool calls together for optimal performance. ALWAYS run the following commands in parallel:
            - Add relevant untracked files to the staging area.
            - Create the commit with a message ending with:
            🤖 Generated with [Claude Code](https://claude.ai/code)

            Co-Authored-By: Claude <239573049@qq.com>
            - Run git status to make sure the commit succeeded.
         4. If the commit fails due to pre-commit hook changes, retry the commit ONCE to include these automated changes. If it fails again, it usually means a pre-commit hook is preventing the commit. If the commit succeeds but you notice that files were modified by the pre-commit hook, you MUST amend your commit to include them.

         Important notes:
         - NEVER update the git config
         - NEVER run additional commands to read or explore code, besides git bash commands
         - NEVER use the TodoWrite or Task tools
         - DO NOT push to the remote repository unless the user explicitly asks you to do so
         - IMPORTANT: Never use git commands with the -i flag (like git rebase -i or git add -i) since they require interactive input which is not supported.
         - If there are no changes to commit (i.e., no untracked files and no modifications), do not create an empty commit
         - In order to ensure good formatting, ALWAYS pass the commit message via a HEREDOC, a la this example:
         <example>
         git commit -m "$(cat <<'EOF'
            Commit message here.

            🤖 Generated with [Claude Code](https://claude.ai/code)

            Co-Authored-By: Claude <239573049@qq.com>
            EOF
            )"
         </example>

         # Creating pull requests
         Use the gh command via the Bash tool for ALL GitHub-related tasks including working with issues, pull requests, checks, and releases. If given a Github URL use the gh command to get the information needed.

         IMPORTANT: When the user asks you to create a pull request, follow these steps carefully:

         1. You have the capability to call multiple tools in a single response. When multiple independent pieces of information are requested, batch your tool calls together for optimal performance. ALWAYS run the following bash commands in parallel using the Bash tool, in order to understand the current state of the branch since it diverged from the main branch:
            - Run a git status command to see all untracked files
            - Run a git diff command to see both staged and unstaged changes that will be committed
            - Check if the current branch tracks a remote branch and is up to date with the remote, so you know if you need to push to the remote
            - Run a git log command and `git diff [base-branch]...HEAD` to understand the full commit history for the current branch (from the time it diverged from the base branch)
         2. Analyze all changes that will be included in the pull request, making sure to look at all relevant commits (NOT just the latest commit, but ALL commits that will be included in the pull request!!!), and draft a pull request summary
         3. You have the capability to call multiple tools in a single response. When multiple independent pieces of information are requested, batch your tool calls together for optimal performance. ALWAYS run the following commands in parallel:
            - Create new branch if needed
            - Push to remote with -u flag if needed
            - Create PR using gh pr create with the format below. Use a HEREDOC to pass the body to ensure correct formatting.
         <example>
         gh pr create --title "the pr title" --body "$(cat <<'EOF'
         ## Summary
         <1-3 bullet points>

         ## Test plan
         [Checklist of TODOs for testing the pull request...]

         🤖 Generated with [Claude Code](https://claude.ai/code)
         EOF
         )"
         </example>

         Important:
         - NEVER update the git config
         - DO NOT use the TodoWrite or Task tools
         - Return the PR URL when you're done, so the user can see it

         # Other common operations
         - View comments on a Github PR: gh api repos/foo/bar/pulls/123/comments
         """)]
    public static async Task<string> RunBashCommand(
        [Description("The command to execute")]
        string command,
        [Description("Clear, concise description of what this command does in 5-10 words, in active voice.")]
        string description,
        [Description("Set to true to run this command in the background. Use BashOutput to read the output later.")]
        bool run_in_background,
        [Description("Optional timeout in milliseconds (max 600000)")]
        int timeout)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return Error("command must be provided.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Error("description must be provided.");
        }

        var timeoutMs = timeout > 0 ? Math.Min(timeout, 600_000) : DefaultTimeoutMs;

        try
        {
            if (run_in_background)
            {
                var process = CreateProcess(command);
                var session = new BashSession(command, description, process);

                if (!process.Start())
                {
                    session.Dispose();
                    return Error("Failed to start bash process.");
                }

                session.BeginCapture();
                Sessions[session.Id] = session;

                return
                    $"""
                     SUCCESS: Started background bash session with id: {session.Id}
                     Use the BashOutput tool with this id to check output as it becomes available.
                     """;
            }
            else
            {
                using var process = CreateProcess(command);

                if (!process.Start())
                {
                    return Error("Failed to start bash process.");
                }

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                using var cts = new CancellationTokenSource(timeoutMs);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    TryTerminate(process);
                    return Error($"Command timed out after {timeoutMs} ms and was terminated.");
                }

                var stdout = (await stdoutTask).TrimEnd();
                var stderr = (await stderrTask).TrimEnd();
                var exitCode = process.ExitCode;

                return exitCode == 0 ? stdout : stderr;
            }
        }
        catch (Exception ex)
        {
            return Error($"Failed to execute bash command: {ex.Message}");
        }
    }

    private static Process CreateProcess(string command)
    {
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        var processStartInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (isWindows)
        {
            processStartInfo.FileName = "cmd.exe";
            processStartInfo.Arguments = $"/C \"{command}\"";
        }
        else
        {
            var escaped = command.Replace("\"", "\\\"");
            processStartInfo.FileName = "/bin/bash";
            processStartInfo.Arguments = $"-lc \"{escaped}\"";
        }

        return new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true,
        };
    }

    public static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static string Error(string message)
    {
        return $"ERROR: {message}";
    }

    public sealed class BashSession : IDisposable
    {
        private readonly ConcurrentQueue<OutputLine> _pending = new();
        private readonly object _disposeLock = new();
        private bool _disposed;

        public BashSession(string command, string description, Process process)
        {
            Id = Guid.NewGuid().ToString("N");
            Command = command;
            Description = description;
            Process = process;

            Process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is { } line)
                {
                    Enqueue(false, line);
                }
            };

            Process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is { } line)
                {
                    Enqueue(true, line);
                }
            };

            Process.Exited += (_, _) => { Enqueue(false, $"[process exited with code {Process.ExitCode}]"); };
        }

        public string Id { get; }
        public string Command { get; }
        public string Description { get; }
        public Process Process { get; }

        public bool IsDrainComplete => _pending.IsEmpty && Process.HasExited;

        public void BeginCapture()
        {
            Process.BeginOutputReadLine();
            Process.BeginErrorReadLine();
        }

        public (string Stdout, string Stderr, bool Completed) Consume(Regex? filter)
        {
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            while (_pending.TryDequeue(out var line))
            {
                if (filter != null && !filter.IsMatch(line.Text))
                {
                    continue;
                }

                if (line.IsStdErr)
                {
                    stderr.AppendLine(line.Text);
                }
                else
                {
                    stdout.AppendLine(line.Text);
                }
            }

            return (stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd(), Process.HasExited);
        }

        private void Enqueue(bool isStdErr, string text)
        {
            if (_disposed)
            {
                return;
            }

            _pending.Enqueue(new OutputLine(isStdErr, text));

            while (_pending.Count > MaxBufferedLines && _pending.TryDequeue(out _))
            {
                // Trim from the head to avoid unbounded growth.
            }
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                try
                {
                    Process.CancelOutputRead();
                    Process.CancelErrorRead();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    Process.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    private readonly record struct OutputLine(bool IsStdErr, string Text);
}