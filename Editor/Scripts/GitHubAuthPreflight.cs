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

    public sealed class GitHubAuthConnectionResult
    {
        public GitHubAuthCheckResult CheckResult { get; private set; }
        public bool ConnectionAttempted { get; private set; }
        public bool ConnectionStarted { get; private set; }
        public string Message { get; private set; }

        internal static GitHubAuthConnectionResult Create(
            GitHubAuthCheckResult checkResult,
            bool connectionAttempted,
            bool connectionStarted,
            string message)
        {
            return new GitHubAuthConnectionResult
            {
                CheckResult = checkResult,
                ConnectionAttempted = connectionAttempted,
                ConnectionStarted = connectionStarted,
                Message = message ?? ""
            };
        }
    }

    public static class GitHubAuthPreflight
    {
        public const string AutomationLogPrefix = "[AIGitHubAutomation]";

        private const int DialogDetailLimit = 900;
        private const string ReadmeAssetPath = "Packages/com.actionfit.githubauth/README.md";
        private const string MacTerminalScriptFileName = "actionfit-ai-github-setup.command";
        private const string LinuxTerminalScriptFileName = "actionfit-ai-github-setup.sh";
        private const string WindowsTerminalScriptFileName = "actionfit-ai-github-setup.ps1";

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

            string rawRemoteUrl = remote.Output.Trim();
            string remoteUrl = SanitizeRemoteUrl(rawRemoteUrl);
            if (!IsGitHubRemote(rawRemoteUrl))
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
                    SanitizeDetails(readCheck.CombinedOutput, rawRemoteUrl, remoteUrl),
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
                    SanitizeDetails(pushCheck.CombinedOutput, rawRemoteUrl, remoteUrl),
                    remoteUrl,
                    "git push --dry-run",
                    failureKind);
            }

            return GitHubAuthCheckResult.Ok("GitHub read and push dry-run checks passed.", remoteUrl);
        }

        public static GitHubAuthConnectionResult CheckAndTryConnectCurrentProject()
        {
            return CheckAndTryConnectProject(GetCurrentUnityProjectRoot());
        }

        public static GitHubAuthConnectionResult CheckAndTryConnectProject(string projectRoot)
        {
            GitHubAuthCheckResult checkResult = CheckProjectGitHubPushAccess(projectRoot);
            if (checkResult.Success)
            {
                return GitHubAuthConnectionResult.Create(
                    checkResult,
                    false,
                    false,
                    "GitHub connection is already available.");
            }

            if (!CanStartConnection(checkResult))
            {
                return GitHubAuthConnectionResult.Create(
                    checkResult,
                    false,
                    false,
                    "Interactive GitHub connection was not started for this failure kind.");
            }

            if (Application.isBatchMode)
            {
                return GitHubAuthConnectionResult.Create(
                    checkResult,
                    false,
                    false,
                    "Interactive GitHub connection is disabled in batchmode.");
            }

            bool started = OpenProjectGitHubSetupTerminal(projectRoot, out string error);
            return GitHubAuthConnectionResult.Create(
                checkResult,
                true,
                started,
                started ? "Interactive GitHub connection terminal started." : error);
        }

        public static void CheckAndTryConnectCurrentProjectForAutomation()
        {
            GitHubAuthConnectionResult result = CheckAndTryConnectCurrentProject();
            GitHubAuthCheckResult checkResult = result.CheckResult;
            Debug.Log(
                $"{AutomationLogPrefix} success={checkResult.Success} " +
                $"connectionAttempted={result.ConnectionAttempted} " +
                $"connectionStarted={result.ConnectionStarted} " +
                $"failureKind={checkResult.FailureKind}");
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
            if (Application.isBatchMode)
            {
                error = "Interactive GitHub connection is disabled in batchmode.";
                return false;
            }

            string resolvedProjectRoot = ResolveProjectRoot(projectRoot);
            if (!Directory.Exists(resolvedProjectRoot))
            {
                error = $"Unity project root is not valid: {resolvedProjectRoot}";
                EditorUtility.DisplayDialog("AI GitHub", error, "OK");
                return false;
            }

#if UNITY_EDITOR_WIN
            string setupScript = BuildPowerShellSetupScript(resolvedProjectRoot);
            if (TryOpenWindowsTerminal(setupScript, out error))
                return true;
#elif UNITY_EDITOR_OSX
            string setupScript = BuildBashSetupScript(resolvedProjectRoot);
            if (TryOpenMacTerminal(setupScript, out error))
                return true;
#elif UNITY_EDITOR_LINUX
            string setupScript = BuildBashSetupScript(resolvedProjectRoot);
            if (TryOpenLinuxTerminal(setupScript, out error))
                return true;
#else
            string setupScript = BuildBashSetupScript(resolvedProjectRoot);
            error = "Automatic terminal launch is not supported on this editor platform.";
#endif
            EditorGUIUtility.systemCopyBuffer = setupScript;
            EditorUtility.DisplayDialog(
                "AI GitHub",
                "터미널 자동 실행에 실패해 AI GitHub 연결 스크립트를 클립보드에 복사했습니다.\n\n" +
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

        internal static string BuildBashSetupScript(string projectRoot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#!/bin/bash");
            builder.AppendLine("clear");
            builder.AppendLine("echo \"ActionFit AI GitHub Setup\"");
            builder.AppendLine("echo \"================================\"");
            builder.AppendLine($"cd {ShellSingleQuote(projectRoot)} || exit 1");
            builder.AppendLine("echo \"Project: $(pwd)\"");
            builder.AppendLine("echo");
            builder.AppendLine("echo \"[1/6] Git origin remote\"");
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
            builder.AppendLine("  echo \"[2/6] SSH GitHub authentication\"");
            builder.AppendLine("  ssh -T git@github.com || true");
            builder.AppendLine("else");
            builder.AppendLine("  echo \"[2/6] GitHub CLI authentication\"");
            builder.AppendLine("  if ! command -v gh >/dev/null 2>&1; then");
            builder.AppendLine("    echo \"GitHub CLI is not installed.\"");
            builder.AppendLine("    echo \"Install GitHub CLI from: https://cli.github.com/\"");
            builder.AppendLine("  else");
            builder.AppendLine("    gh auth status --hostname github.com || gh auth login --hostname github.com --git-protocol https --scopes repo,workflow");
            builder.AppendLine("    echo");
            builder.AppendLine("    echo \"[3/6] Configure git credential helper for GitHub CLI\"");
            builder.AppendLine("    gh auth setup-git --hostname github.com");
            builder.AppendLine("    echo");
            builder.AppendLine("    echo \"[4/6] Reuse the GitHub credential across github.com repositories\"");
            builder.AppendLine("    git config --global credential.https://github.com.useHttpPath false");
            builder.AppendLine("  fi");
            builder.AppendLine("fi");
            builder.AppendLine("echo");
            builder.AppendLine("echo \"[5/6] Check repository read access\"");
            builder.AppendLine("GIT_TERMINAL_PROMPT=0 git ls-remote origin HEAD");
            builder.AppendLine("read_status=$?");
            builder.AppendLine("echo");
            builder.AppendLine("echo \"[6/6] Check current branch push access with dry-run\"");
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

        internal static string BuildPowerShellSetupScript(string projectRoot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("$ErrorActionPreference = 'Continue'");
            builder.AppendLine("Clear-Host");
            builder.AppendLine("Write-Host 'ActionFit AI GitHub Setup'");
            builder.AppendLine("Write-Host '================================'");
            builder.AppendLine($"Set-Location -LiteralPath {PowerShellSingleQuote(projectRoot)}");
            builder.AppendLine("Write-Host \"Project: $(Get-Location)\"");
            builder.AppendLine("Write-Host ''");
            builder.AppendLine("Write-Host '[1/6] Git origin remote'");
            builder.AppendLine("$originUrl = (& git remote get-url origin 2>$null | Select-Object -First 1)");
            builder.AppendLine("if ([string]::IsNullOrWhiteSpace($originUrl)) {");
            builder.AppendLine("  Write-Host 'No origin remote is configured. Set origin first, then retry.' -ForegroundColor Yellow");
            builder.AppendLine("  Read-Host 'Press Enter to close'");
            builder.AppendLine("  exit 1");
            builder.AppendLine("}");
            builder.AppendLine("Write-Host 'origin is configured.'");
            builder.AppendLine("Write-Host ''");
            builder.AppendLine("if ($originUrl -match '^(git@github\\.com:|ssh://git@github\\.com/)') {");
            builder.AppendLine("  Write-Host '[2/6] SSH GitHub authentication'");
            builder.AppendLine("  & ssh -T git@github.com");
            builder.AppendLine("} else {");
            builder.AppendLine("  Write-Host '[2/6] GitHub CLI authentication'");
            builder.AppendLine("  if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {");
            builder.AppendLine("    Write-Host 'GitHub CLI is not installed.' -ForegroundColor Yellow");
            builder.AppendLine("    Write-Host 'Install GitHub CLI from: https://cli.github.com/'");
            builder.AppendLine("  } else {");
            builder.AppendLine("    & gh auth status --hostname github.com");
            builder.AppendLine("    if ($LASTEXITCODE -ne 0) {");
            builder.AppendLine("      & gh auth login --hostname github.com --git-protocol https --scopes 'repo,workflow'");
            builder.AppendLine("    }");
            builder.AppendLine("    if ($LASTEXITCODE -eq 0) {");
            builder.AppendLine("      Write-Host ''");
            builder.AppendLine("      Write-Host '[3/6] Configure git credential helper for GitHub CLI'");
            builder.AppendLine("      & gh auth setup-git --hostname github.com");
            builder.AppendLine("      Write-Host ''");
            builder.AppendLine("      Write-Host '[4/6] Reuse the GitHub credential across github.com repositories'");
            builder.AppendLine("      & git config --global credential.https://github.com.useHttpPath false");
            builder.AppendLine("    }");
            builder.AppendLine("  }");
            builder.AppendLine("}");
            builder.AppendLine("Write-Host ''");
            builder.AppendLine("Write-Host '[5/6] Check repository read access'");
            builder.AppendLine("$env:GIT_TERMINAL_PROMPT = '0'");
            builder.AppendLine("& git ls-remote origin HEAD | Out-Host");
            builder.AppendLine("$readStatus = $LASTEXITCODE");
            builder.AppendLine("Write-Host ''");
            builder.AppendLine("Write-Host '[6/6] Check current branch push access with dry-run'");
            builder.AppendLine("$pushOutput = (& git push --dry-run 2>&1 | Out-String)");
            builder.AppendLine("$pushStatus = $LASTEXITCODE");
            builder.AppendLine("Write-Host $pushOutput");
            builder.AppendLine("if ($readStatus -eq 0 -and $pushStatus -eq 0) {");
            builder.AppendLine("  Write-Host 'GitHub read and push dry-run checks passed.' -ForegroundColor Green");
            builder.AppendLine("} elseif ($pushOutput -match 'non-fast-forward|fetch first|tip of your current branch is behind') {");
            builder.AppendLine("  Write-Host 'GitHub connection succeeded, but the current branch is behind its remote counterpart.' -ForegroundColor Yellow");
            builder.AppendLine("  Write-Host 'Review local changes, then run: git pull --ff-only'");
            builder.AppendLine("} else {");
            builder.AppendLine("  Write-Host 'GitHub checks did not fully pass.' -ForegroundColor Yellow");
            builder.AppendLine("  Write-Host 'If the current branch has no upstream, run: git push -u origin <branch>'");
            builder.AppendLine("  Write-Host 'If permission is denied, check GitHub repo/org permission for this account.'");
            builder.AppendLine("}");
            builder.AppendLine("Write-Host ''");
            builder.AppendLine("Read-Host 'Press Enter to close'");
            return builder.ToString();
        }

#if UNITY_EDITOR_WIN
        private static bool TryOpenWindowsTerminal(string setupScript, out string error)
        {
            error = "";
            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "ActionFitGitHubAuth");
                Directory.CreateDirectory(directory);
                string scriptPath = Path.Combine(directory, WindowsTerminalScriptFileName);
                File.WriteAllText(scriptPath, setupScript, new UTF8Encoding(true));

                return StartDetachedProcess(
                    "powershell.exe",
                    $"-NoLogo -NoProfile -ExecutionPolicy Bypass -File {QuoteWindowsArgument(scriptPath)}",
                    out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogError($"[GitHubAuthPreflight] PowerShell setup failed: {exception.Message}");
                return false;
            }
        }
#endif

#if UNITY_EDITOR_OSX
        private static bool TryOpenMacTerminal(string setupScript, out string error)
        {
            error = "";
            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "ActionFitGitHubAuth");
                Directory.CreateDirectory(directory);
                string scriptPath = Path.Combine(directory, MacTerminalScriptFileName);
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
#endif

#if UNITY_EDITOR_LINUX
        private static bool TryOpenLinuxTerminal(string setupScript, out string error)
        {
            error = "";
            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "ActionFitGitHubAuth");
                Directory.CreateDirectory(directory);
                string scriptPath = Path.Combine(directory, LinuxTerminalScriptFileName);
                File.WriteAllText(scriptPath, setupScript, new UTF8Encoding(false));

                if (!RunProcess("/bin/chmod", $"+x {QuoteProcessArgument(scriptPath)}", out error))
                    return false;

                string quotedScriptPath = QuoteProcessArgument(scriptPath);
                if (StartDetachedProcess("x-terminal-emulator", $"-e /bin/bash {quotedScriptPath}", out _))
                    return true;
                if (StartDetachedProcess("gnome-terminal", $"-- /bin/bash {quotedScriptPath}", out _))
                    return true;
                if (StartDetachedProcess("konsole", $"-e /bin/bash {quotedScriptPath}", out _))
                    return true;

                error = "No supported Linux terminal launcher was found.";
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogError($"[GitHubAuthPreflight] Linux terminal setup failed: {exception.Message}");
                return false;
            }
        }
#endif

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

        private static bool StartDetachedProcess(string fileName, string arguments, out string error)
        {
            error = "";
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                Process process = Process.Start(startInfo);
                if (process == null)
                {
                    error = $"Failed to start {fileName}.";
                    return false;
                }

                process.Dispose();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string ShellSingleQuote(string value)
        {
            return "'" + (value ?? "").Replace("'", "'\"'\"'") + "'";
        }

        private static string PowerShellSingleQuote(string value)
        {
            return "'" + (value ?? "").Replace("'", "''") + "'";
        }

        private static string QuoteProcessArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string QuoteWindowsArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static bool CanStartConnection(GitHubAuthCheckResult result)
        {
            return result != null &&
                   !result.Success &&
                   result.FailureKind != GitHubAuthCheckFailureKind.BranchOutOfDate &&
                   IsGitHubRemote(result.RemoteUrl);
        }

        internal static bool IsGitHubRemote(string remoteUrl)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl))
                return false;

            string trimmed = remoteUrl.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri))
                return string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);

            return trimmed.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase);
        }

        internal static string SanitizeRemoteUrl(string remoteUrl)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl))
                return "";

            if (Uri.TryCreate(remoteUrl, UriKind.Absolute, out Uri uri) &&
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                var builder = new UriBuilder(uri)
                {
                    UserName = "",
                    Password = ""
                };
                return builder.Uri.AbsoluteUri;
            }

            return remoteUrl;
        }

        private static string SanitizeDetails(string details, string rawRemoteUrl, string sanitizedRemoteUrl)
        {
            if (string.IsNullOrEmpty(details) ||
                string.IsNullOrEmpty(rawRemoteUrl) ||
                string.Equals(rawRemoteUrl, sanitizedRemoteUrl, StringComparison.Ordinal))
            {
                return details ?? "";
            }

            return details.Replace(rawRemoteUrl, sanitizedRemoteUrl);
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
