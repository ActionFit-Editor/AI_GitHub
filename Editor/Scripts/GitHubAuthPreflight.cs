#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ActionFit.GitHubAuth.Editor
{
    public enum GitHubAuthCheckFailureKind
    {
        None,
        General,
        BranchOutOfDate
    }

    public sealed class GitHubAuthCheckResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public string Details { get; private set; }
        public string RemoteUrl { get; private set; }
        public string FailedCommand { get; private set; }
        public GitHubAuthCheckFailureKind FailureKind { get; private set; }

        public static GitHubAuthCheckResult Ok(string message, string remoteUrl)
        {
            return new GitHubAuthCheckResult
            {
                Success = true,
                Message = message,
                RemoteUrl = remoteUrl ?? "",
                Details = "",
                FailedCommand = "",
                FailureKind = GitHubAuthCheckFailureKind.None
            };
        }

        public static GitHubAuthCheckResult Fail(
            string message,
            string details,
            string remoteUrl = "",
            string failedCommand = "",
            GitHubAuthCheckFailureKind failureKind = GitHubAuthCheckFailureKind.General)
        {
            return new GitHubAuthCheckResult
            {
                Success = false,
                Message = message ?? "GitHub authentication check failed.",
                Details = details ?? "",
                RemoteUrl = remoteUrl ?? "",
                FailedCommand = failedCommand ?? "",
                FailureKind = failureKind
            };
        }
    }

    public static class GitHubAuthPreflight
    {
        private const int DialogDetailLimit = 900;
        private const string ReadmeAssetPath = "Packages/com.actionfit.githubauth/README.md";
        private const string TerminalScriptFileName = "actionfit-github-auth-setup.command";

        public static string GetCurrentUnityProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        public static GitHubAuthCheckResult CheckProjectGitHubPushAccess(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
            {
                return GitHubAuthCheckResult.Fail(
                    "Unity project root is not valid.",
                    $"projectRoot={projectRoot}");
            }

            GitCommandResult remote = RunGit(projectRoot, "remote get-url origin");
            if (!remote.Success)
            {
                return GitHubAuthCheckResult.Fail(
                    "Git origin remote is not configured.",
                    remote.CombinedOutput,
                    failedCommand: "git remote get-url origin");
            }

            string remoteUrl = remote.Output.Trim();
            if (!IsGitHubRemote(remoteUrl))
            {
                return GitHubAuthCheckResult.Fail(
                    "Origin remote is not a GitHub repository.",
                    $"origin={remoteUrl}",
                    remoteUrl,
                    "git remote get-url origin");
            }

            GitCommandResult readCheck = RunGit(projectRoot, "ls-remote origin HEAD");
            if (!readCheck.Success)
            {
                return GitHubAuthCheckResult.Fail(
                    "GitHub repository access is not available from this machine.",
                    readCheck.CombinedOutput,
                    remoteUrl,
                    "git ls-remote origin HEAD");
            }

            GitCommandResult pushCheck = RunGit(projectRoot, "push --dry-run");
            if (!pushCheck.Success)
            {
                GitHubAuthCheckFailureKind failureKind = ClassifyPushFailure(pushCheck.CombinedOutput);
                string message = failureKind == GitHubAuthCheckFailureKind.BranchOutOfDate
                    ? "Current branch is behind its remote counterpart."
                    : "GitHub push access is not available from this machine.";

                return GitHubAuthCheckResult.Fail(
                    message,
                    pushCheck.CombinedOutput,
                    remoteUrl,
                    "git push --dry-run",
                    failureKind);
            }

            return GitHubAuthCheckResult.Ok("GitHub read and push dry-run checks passed.", remoteUrl);
        }

        public static bool EnsureProjectGitHubPushAccess(string projectRoot, string contextName)
        {
            return EnsureProjectGitHubPushAccess(projectRoot, contextName, out _);
        }

        public static bool EnsureProjectGitHubPushAccess(
            string projectRoot,
            string contextName,
            out GitHubAuthCheckResult result)
        {
            result = CheckProjectGitHubPushAccess(projectRoot);
            if (result.Success)
                return true;

            ShowRequiredDialog(result, contextName, projectRoot);
            return false;
        }

        public static void ShowRequiredDialog(GitHubAuthCheckResult result, string contextName)
        {
            ShowRequiredDialog(result, contextName, "");
        }

        public static void ShowRequiredDialog(GitHubAuthCheckResult result, string contextName, string projectRoot)
        {
            if (result?.FailureKind == GitHubAuthCheckFailureKind.BranchOutOfDate)
            {
                ShowBranchSyncRequiredDialog(result, contextName);
                return;
            }

            string title = "GitHub 인증 필요";
            string context = string.IsNullOrWhiteSpace(contextName) ? "이 작업" : contextName;
            string failedCommand = string.IsNullOrEmpty(result?.FailedCommand)
                ? ""
                : $"\n실패 단계: {result.FailedCommand}\n";
            string details = Shorten(result?.Details ?? "", DialogDetailLimit);

            string message =
                $"{context}을 실행하려면 이 기기에서 GitHub 인증과 push/tag 권한이 필요합니다.\n\n" +
                "Unity 프로젝트 루트 터미널에서 아래 순서로 확인하세요.\n\n" +
                "git remote -v\n" +
                "gh auth status --hostname github.com\n" +
                "gh auth login --hostname github.com --git-protocol https --scopes repo,workflow\n" +
                "gh auth setup-git --hostname github.com\n" +
                "GIT_TERMINAL_PROMPT=0 git ls-remote origin HEAD\n" +
                "GIT_TERMINAL_PROMPT=0 git push --dry-run\n\n" +
                "`연결 시도`를 누르면 Terminal을 열고 위 절차를 현재 프로젝트 루트에서 실행합니다.\n" +
                "자세한 내용은 `Packages/com.actionfit.githubauth/README.md`를 참고하거나, AI에게 GitHub 인증 가이드를 문의하세요." +
                failedCommand;

            if (!string.IsNullOrEmpty(details))
                message += $"\n오류 내용:\n{details}";

            int selected = EditorUtility.DisplayDialogComplex(title, message, "연결 시도", "닫기", "README 열기");
            if (selected == 0)
            {
                OpenProjectGitHubSetupTerminal(projectRoot);
            }
            else if (selected == 2)
            {
                OpenReadme();
            }
        }

        private static void ShowBranchSyncRequiredDialog(GitHubAuthCheckResult result, string contextName)
        {
            string context = string.IsNullOrWhiteSpace(contextName) ? "이 작업" : contextName;
            string details = Shorten(result?.Details ?? "", DialogDetailLimit);
            string message =
                $"GitHub 연결은 확인됐지만 현재 로컬 브랜치가 원격 브랜치보다 뒤처져 있어 {context}을 실행할 수 없습니다.\n\n" +
                "현재 변경사항을 확인한 뒤 원격 변경을 통합하고 다시 시도하세요.\n\n" +
                "git status --short --branch\n" +
                "git pull --ff-only\n\n" +
                "`git pull --ff-only`가 거부되면 미커밋 변경 또는 브랜치 분기 상태를 먼저 정리해야 합니다.";

            if (!string.IsNullOrEmpty(details))
                message += $"\n\n오류 내용:\n{details}";

            EditorUtility.DisplayDialog("로컬 브랜치 동기화 필요", message, "확인");
        }

        public static bool OpenProjectGitHubSetupTerminal(string projectRoot)
        {
            return OpenProjectGitHubSetupTerminal(projectRoot, out _);
        }

        public static bool OpenProjectGitHubSetupTerminal(string projectRoot, out string error)
        {
            error = "";
            string resolvedProjectRoot = ResolveProjectRoot(projectRoot);
            if (!Directory.Exists(resolvedProjectRoot))
            {
                error = $"Unity project root is not valid: {resolvedProjectRoot}";
                EditorUtility.DisplayDialog("GitHub Auth", error, "OK");
                return false;
            }

            string setupScript = BuildSetupScript(resolvedProjectRoot);
#if UNITY_EDITOR_OSX
            if (TryOpenMacTerminal(setupScript, out error))
                return true;
#else
            error = "Automatic Terminal launch is only supported on macOS editor.";
#endif
            EditorGUIUtility.systemCopyBuffer = setupScript;
            EditorUtility.DisplayDialog(
                "GitHub Auth",
                "터미널 자동 실행에 실패해 GitHub 연결 스크립트를 클립보드에 복사했습니다.\n\n" +
                "터미널을 직접 열고 붙여넣어 실행하세요.\n\n" +
                error,
                "OK");
            return false;
        }

        public static void OpenReadme()
        {
            var readme = AssetDatabase.LoadAssetAtPath<TextAsset>(ReadmeAssetPath);
            if (readme != null)
            {
                AssetDatabase.OpenAsset(readme);
                return;
            }

            string fullPath = Path.Combine(GetCurrentUnityProjectRoot(), ReadmeAssetPath);
            if (File.Exists(fullPath))
                EditorUtility.RevealInFinder(fullPath);
        }

        private static string ResolveProjectRoot(string projectRoot)
        {
            return Path.GetFullPath(string.IsNullOrWhiteSpace(projectRoot)
                ? GetCurrentUnityProjectRoot()
                : projectRoot);
        }

        private static string BuildSetupScript(string projectRoot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#!/bin/bash");
            builder.AppendLine("clear");
            builder.AppendLine("echo \"ActionFit GitHub Auth Setup\"");
            builder.AppendLine("echo \"================================\"");
            builder.AppendLine($"cd {ShellSingleQuote(projectRoot)} || exit 1");
            builder.AppendLine("echo \"Project: $(pwd)\"");
            builder.AppendLine("echo");
            builder.AppendLine("echo \"[1/5] Git origin remote\"");
            builder.AppendLine("git remote -v || true");
            builder.AppendLine("origin_url=\"$(git remote get-url origin 2>/dev/null || true)\"");
            builder.AppendLine("if [[ -z \"$origin_url\" ]]; then");
            builder.AppendLine("  echo \"No origin remote is configured. Set origin first, then retry.\"");
            builder.AppendLine("  echo");
            builder.AppendLine("  read -n 1 -s -r -p \"Press any key to close...\"");
            builder.AppendLine("  echo");
            builder.AppendLine("  exit 1");
            builder.AppendLine("fi");
            builder.AppendLine("echo");
            builder.AppendLine("if [[ \"$origin_url\" == git@github.com:* || \"$origin_url\" == ssh://git@github.com/* ]]; then");
            builder.AppendLine("  echo \"[2/5] SSH GitHub authentication\"");
            builder.AppendLine("  ssh -T git@github.com || true");
            builder.AppendLine("else");
            builder.AppendLine("  echo \"[2/5] GitHub CLI authentication\"");
            builder.AppendLine("  if ! command -v gh >/dev/null 2>&1; then");
            builder.AppendLine("    echo \"GitHub CLI is not installed.\"");
            builder.AppendLine("    echo \"Install it with: brew install gh\"");
            builder.AppendLine("  else");
            builder.AppendLine("    gh auth status --hostname github.com || gh auth login --hostname github.com --git-protocol https --scopes repo,workflow");
            builder.AppendLine("    echo");
            builder.AppendLine("    echo \"[3/5] Configure git credential helper for GitHub CLI\"");
            builder.AppendLine("    gh auth setup-git --hostname github.com");
            builder.AppendLine("  fi");
            builder.AppendLine("fi");
            builder.AppendLine("echo");
            builder.AppendLine("echo \"[4/5] Check repository read access\"");
            builder.AppendLine("GIT_TERMINAL_PROMPT=0 git ls-remote origin HEAD");
            builder.AppendLine("read_status=$?");
            builder.AppendLine("echo");
            builder.AppendLine("echo \"[5/5] Check current branch push access with dry-run\"");
            builder.AppendLine("push_output=\"$(LC_ALL=C GIT_TERMINAL_PROMPT=0 git push --dry-run 2>&1)\"");
            builder.AppendLine("push_status=$?");
            builder.AppendLine("printf '%s\\n' \"$push_output\"");
            builder.AppendLine("echo");
            builder.AppendLine("if [[ $read_status -eq 0 && $push_status -eq 0 ]]; then");
            builder.AppendLine("  echo \"GitHub read and push dry-run checks passed.\"");
            builder.AppendLine("elif [[ \"$push_output\" == *\"non-fast-forward\"* || \"$push_output\" == *\"fetch first\"* || \"$push_output\" == *\"tip of your current branch is behind\"* ]]; then");
            builder.AppendLine("  echo \"GitHub connection succeeded, but the current branch is behind its remote counterpart.\"");
            builder.AppendLine("  echo \"Review local changes, then run: git pull --ff-only\"");
            builder.AppendLine("else");
            builder.AppendLine("  echo \"GitHub checks did not fully pass.\"");
            builder.AppendLine("  echo \"If the current branch has no upstream, run: git push -u origin $(git branch --show-current)\"");
            builder.AppendLine("  echo \"If permission is denied, check GitHub repo/org permission for this account.\"");
            builder.AppendLine("fi");
            builder.AppendLine("echo");
            builder.AppendLine("read -n 1 -s -r -p \"Press any key to close...\"");
            builder.AppendLine("echo");
            return builder.ToString();
        }

#if UNITY_EDITOR_OSX
        private static bool TryOpenMacTerminal(string setupScript, out string error)
        {
            error = "";
            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "ActionFitGitHubAuth");
                Directory.CreateDirectory(directory);
                string scriptPath = Path.Combine(directory, TerminalScriptFileName);
                File.WriteAllText(scriptPath, setupScript, new UTF8Encoding(false));

                if (!RunProcess("/bin/chmod", $"+x {QuoteProcessArgument(scriptPath)}", out error))
                    return false;

                return RunProcess("/usr/bin/open", $"-a Terminal {QuoteProcessArgument(scriptPath)}", out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogError($"[GitHubAuthPreflight] Terminal setup failed: {exception}");
                return false;
            }
        }

        private static bool RunProcess(string fileName, string arguments, out string error)
        {
            error = "";
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode == 0) return true;

                    error = string.IsNullOrWhiteSpace(stderr) ? output.Trim() : stderr.Trim();
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
#endif

        private static string ShellSingleQuote(string value)
        {
            return "'" + (value ?? "").Replace("'", "'\"'\"'") + "'";
        }

        private static string QuoteProcessArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static bool IsGitHubRemote(string remoteUrl)
        {
            return !string.IsNullOrEmpty(remoteUrl) &&
                   remoteUrl.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static GitHubAuthCheckFailureKind ClassifyPushFailure(string output)
        {
            if (ContainsIgnoreCase(output, "non-fast-forward") ||
                ContainsIgnoreCase(output, "fetch first") ||
                ContainsIgnoreCase(output, "tip of your current branch is behind"))
            {
                return GitHubAuthCheckFailureKind.BranchOutOfDate;
            }

            return GitHubAuthCheckFailureKind.General;
        }

        private static bool ContainsIgnoreCase(string value, string expected)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static GitCommandResult RunGit(string workingDirectory, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                startInfo.EnvironmentVariables["LC_ALL"] = "C";

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    return new GitCommandResult(
                        process.ExitCode == 0,
                        output.Trim(),
                        error.Trim());
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GitHubAuthPreflight] git {arguments} failed: {exception.Message}");
                return new GitCommandResult(false, "", exception.Message);
            }
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? "";

            return value.Substring(0, maxLength) + "\n...";
        }

        private sealed class GitCommandResult
        {
            public GitCommandResult(bool success, string output, string error)
            {
                Success = success;
                Output = output ?? "";
                Error = error ?? "";
            }

            public bool Success { get; }
            public string Output { get; }
            public string Error { get; }

            public string CombinedOutput
            {
                get
                {
                    var builder = new StringBuilder();
                    if (!string.IsNullOrEmpty(Output))
                        builder.AppendLine(Output);
                    if (!string.IsNullOrEmpty(Error))
                        builder.AppendLine(Error);
                    return builder.ToString().Trim();
                }
            }
        }
    }
}

#endif
