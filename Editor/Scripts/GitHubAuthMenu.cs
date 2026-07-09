#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace ActionFit.GitHubAuth.Editor
{
    public static class GitHubAuthMenu
    {
        [MenuItem("Tools/Package/GitHub Auth/Check Project GitHub Access", false, 20)]
        public static void CheckProjectGitHubAccess()
        {
            string projectRoot = GitHubAuthPreflight.GetCurrentUnityProjectRoot();
            GitHubAuthCheckResult result = GitHubAuthPreflight.CheckProjectGitHubPushAccess(projectRoot);
            if (result.Success)
            {
                EditorUtility.DisplayDialog(
                    "GitHub Auth",
                    $"GitHub 연결 확인이 완료되었습니다.\n\norigin: {result.RemoteUrl}\n{result.Message}",
                    "OK");
                return;
            }

            GitHubAuthPreflight.ShowRequiredDialog(result, "GitHub 연결 확인", projectRoot);
        }

        [MenuItem("Tools/Package/GitHub Auth/Open Setup Terminal", false, 21)]
        public static void OpenSetupTerminal()
        {
            string projectRoot = GitHubAuthPreflight.GetCurrentUnityProjectRoot();
            if (!GitHubAuthPreflight.OpenProjectGitHubSetupTerminal(projectRoot, out string error))
                Debug.LogWarning($"[GitHubAuthMenu] Failed to open setup terminal: {error}");
        }
    }
}

#endif
