#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace ActionFit.GitHubAuth.Editor
{
    public static class GitHubAuthMenu
    {
        [MenuItem("Tools/ActionFit/GitHub Auth/Check Project GitHub Access")]
        public static void CheckProjectGitHubAccess()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            GitHubAuthCheckResult result = GitHubAuthPreflight.CheckProjectGitHubPushAccess(projectRoot);
            if (result.Success)
            {
                EditorUtility.DisplayDialog(
                    "GitHub Auth",
                    $"GitHub 연결 확인이 완료되었습니다.\n\norigin: {result.RemoteUrl}\n{result.Message}",
                    "OK");
                return;
            }

            GitHubAuthPreflight.ShowRequiredDialog(result, "GitHub 연결 확인");
        }
    }
}

#endif
