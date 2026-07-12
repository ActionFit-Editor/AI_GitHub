#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace ActionFit.GitHubAuth.Editor
{
    public static class GitHubAuthMenu
    {
        [MenuItem("Tools/Package/AI GitHub/Connect Current Project", false, 20)]
        public static void ConnectCurrentProject()
        {
            GitHubAuthConnectionResult result = GitHubAuthPreflight.CheckAndTryConnectCurrentProject();
            if (result.CheckResult.Success)
            {
                EditorUtility.DisplayDialog(
                    "AI GitHub",
                    "GitHub 연결이 이미 설정되어 있습니다.",
                    "OK");
                return;
            }

            if (result.ConnectionStarted)
            {
                Debug.Log("[AI GitHub] Interactive connection terminal started.");
                return;
            }

            if (!result.ConnectionAttempted)
            {
                GitHubAuthPreflight.ShowRequiredDialog(
                    result.CheckResult,
                    "AI GitHub 연결",
                    GitHubAuthPreflight.GetCurrentUnityProjectRoot());
                return;
            }

            Debug.LogWarning($"[AI GitHub] Connection terminal could not be started: {result.Message}");
        }

        [MenuItem("Tools/Package/AI GitHub/Check Project GitHub Access", false, 21)]
        public static void CheckProjectGitHubAccess()
        {
            string projectRoot = GitHubAuthPreflight.GetCurrentUnityProjectRoot();
            GitHubAuthCheckResult result = GitHubAuthPreflight.CheckProjectGitHubPushAccess(projectRoot);
            if (result.Success)
            {
                EditorUtility.DisplayDialog(
                    "AI GitHub",
                    $"GitHub 연결 확인이 완료되었습니다.\n\norigin: {result.RemoteUrl}\n{result.Message}",
                    "OK");
                return;
            }

            GitHubAuthPreflight.ShowRequiredDialog(result, "GitHub 연결 확인", projectRoot);
        }

        [MenuItem("Tools/Package/AI GitHub/Open Setup Terminal", false, 22)]
        public static void OpenSetupTerminal()
        {
            string projectRoot = GitHubAuthPreflight.GetCurrentUnityProjectRoot();
            if (!GitHubAuthPreflight.OpenProjectGitHubSetupTerminal(projectRoot, out string error))
                Debug.LogWarning($"[GitHubAuthMenu] Failed to open setup terminal: {error}");
        }
    }

    [InitializeOnLoad]
    internal static class GitHubAuthFirstInstallBootstrap
    {
        private const string FirstInstallCheckKeyPrefix = "ActionFit.AIGitHub.FirstInstallCheck";

        static GitHubAuthFirstInstallBootstrap()
        {
            EditorApplication.delayCall += CheckFirstInstallConnection;
        }

        private static void CheckFirstInstallConnection()
        {
            string projectRoot = GitHubAuthPreflight.GetCurrentUnityProjectRoot();
            string checkKey = $"{FirstInstallCheckKeyPrefix}.{Hash128.Compute(projectRoot)}";
            if (Application.isBatchMode || EditorPrefs.GetBool(checkKey, false))
                return;

            EditorPrefs.SetBool(checkKey, true);
            GitHubAuthConnectionResult result = GitHubAuthPreflight.CheckAndTryConnectProject(projectRoot);
            if (result.CheckResult.Success)
            {
                Debug.Log("[AI GitHub] First-install GitHub connection check passed.");
                return;
            }

            if (result.ConnectionStarted)
            {
                Debug.Log("[AI GitHub] First-install check started an interactive connection terminal.");
                return;
            }

            Debug.LogWarning(
                $"[AI GitHub] First-install connection check did not pass. " +
                $"failureKind={result.CheckResult.FailureKind}, connectionAttempted={result.ConnectionAttempted}. " +
                "Use Tools/Package/AI GitHub/Connect Current Project when the project origin is ready.");
        }
    }
}

#endif
