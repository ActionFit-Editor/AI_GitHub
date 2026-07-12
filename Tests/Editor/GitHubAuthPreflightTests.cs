#if UNITY_EDITOR

using NUnit.Framework;

namespace ActionFit.GitHubAuth.Editor.Tests
{
    public class GitHubAuthPreflightTests
    {
        [TestCase("! [rejected] dev_jewoo -> dev_jewoo (non-fast-forward)")]
        [TestCase("Updates were rejected because the remote contains work that you do not have locally. (fetch first)")]
        [TestCase("the tip of your current branch is behind its remote counterpart")]
        public void ClassifyPushFailureDetectsBranchOutOfDate(string output)
        {
            Assert.That(
                GitHubAuthPreflight.ClassifyPushFailure(output),
                Is.EqualTo(GitHubAuthCheckFailureKind.BranchOutOfDate));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("remote: Permission to repository denied")]
        [TestCase("fatal: could not read Username for 'https://github.com'")]
        public void ClassifyPushFailureKeepsOtherFailuresGeneral(string output)
        {
            Assert.That(
                GitHubAuthPreflight.ClassifyPushFailure(output),
                Is.EqualTo(GitHubAuthCheckFailureKind.General));
        }

        [Test]
        public void SetupScriptsConfigureSharedGitHubCredentialWithoutPersistingToken()
        {
            string bash = GitHubAuthPreflight.BuildBashSetupScript("/tmp/project");
            string powershell = GitHubAuthPreflight.BuildPowerShellSetupScript("C:\\project");

            foreach (string script in new[] { bash, powershell })
            {
                StringAssert.Contains("gh auth login", script);
                StringAssert.Contains("gh auth setup-git", script);
                StringAssert.Contains("credential.https://github.com.useHttpPath false", script);
                StringAssert.DoesNotContain("gh auth token", script);
                StringAssert.DoesNotContain("EditorPrefs", script);
                StringAssert.DoesNotContain("PlayerPrefs", script);
            }
        }

        [TestCase("https://secret-value@github.com/ActionFit-Editor/Repo.git", "https://github.com/ActionFit-Editor/Repo.git")]
        [TestCase("https://user:secret-value@github.com/ActionFit-Editor/Repo.git", "https://github.com/ActionFit-Editor/Repo.git")]
        [TestCase("git@github.com:ActionFit-Editor/Repo.git", "git@github.com:ActionFit-Editor/Repo.git")]
        public void SanitizeRemoteUrlRemovesEmbeddedCredential(string remoteUrl, string expected)
        {
            Assert.That(GitHubAuthPreflight.SanitizeRemoteUrl(remoteUrl), Is.EqualTo(expected));
        }

        [TestCase("https://github.com/ActionFit-Editor/Repo.git", true)]
        [TestCase("git@github.com:ActionFit-Editor/Repo.git", true)]
        [TestCase("ssh://git@github.com/ActionFit-Editor/Repo.git", true)]
        [TestCase("https://example.com/?next=github.com", false)]
        [TestCase("https://notgithub.com/ActionFit-Editor/Repo.git", false)]
        public void IsGitHubRemoteRequiresExactGitHubHost(string remoteUrl, bool expected)
        {
            Assert.That(GitHubAuthPreflight.IsGitHubRemote(remoteUrl), Is.EqualTo(expected));
        }
    }
}

#endif
