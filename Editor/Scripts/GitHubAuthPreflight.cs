#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace ActionFit.GitHubAuth.Editor
{
    public sealed class GitHubAuthCheckResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public string Details { get; private set; }
        public string RemoteUrl { get; private set; }
        public string FailedCommand { get; private set; }

        public static GitHubAuthCheckResult Ok(string message, string remoteUrl)
        {
            return new GitHubAuthCheckResult
            {
                Success = true,
                Message = message,
                RemoteUrl = remoteUrl ?? "",
                Details = "",
                FailedCommand = ""
            };
        }

        public static GitHubAuthCheckResult Fail(string message, string details, string remoteUrl = "", string failedCommand = "")
        {
            return new GitHubAuthCheckResult
            {
                Success = false,
                Message = message ?? "GitHub authentication check failed.",
                Details = details ?? "",
                RemoteUrl = remoteUrl ?? "",
                FailedCommand = failedCommand ?? ""
            };
        }
    }

    public static class GitHubAuthPreflight
    {
        private const int DialogDetailLimit = 900;

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
                return GitHubAuthCheckResult.Fail(
                    "GitHub push access is not available from this machine.",
                    pushCheck.CombinedOutput,
                    remoteUrl,
                    "git push --dry-run");
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

            ShowRequiredDialog(result, contextName);
            return false;
        }

        public static void ShowRequiredDialog(GitHubAuthCheckResult result, string contextName)
        {
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
                "자세한 내용은 `Packages/com.actionfit.githubauth/README.md`를 참고하거나, AI에게 GitHub 인증 가이드를 문의하세요." +
                failedCommand;

            if (!string.IsNullOrEmpty(details))
                message += $"\n오류 내용:\n{details}";

            EditorUtility.DisplayDialog(title, message, "OK");
        }

        private static bool IsGitHubRemote(string remoteUrl)
        {
            return !string.IsNullOrEmpty(remoteUrl) &&
                   remoteUrl.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0;
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
