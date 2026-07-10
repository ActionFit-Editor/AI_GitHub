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
    }
}

#endif
