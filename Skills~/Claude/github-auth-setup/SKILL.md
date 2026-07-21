---
name: github-auth-setup
description: Connect one exact project to GitHub through the package-owned visible terminal flow after safe diagnosis and explicit approval, then verify read and push-dry-run access without exposing credentials. Use for initial GitHub authentication or recoverable local auth setup failures.
disable-model-invocation: true
---

# Set Up GitHub Access

This workflow may open an interactive login, change GitHub CLI or OS credential state, and update GitHub-specific global Git configuration. Run only for an explicitly selected project after approval of the exact connection plan.

1. Read `PACKAGE_SKILLS.md`, the package `README.md` and `AI_GUIDE.md`, and the consuming repository's safety and worktree instructions. Resolve the exact Git root and Unity project path.
2. Perform the same read-only, privacy-safe checks as `$github-auth-diagnose`: classify the `origin` kind without reproducing its URL, GitHub CLI availability/authentication, remote read access, current-branch push dry-run, and branch synchronization. Do not print credential-helper output, environment dumps, account identity, raw Git errors, or tokens.
3. Stop without changes when read and push-dry-run access already pass. Treat a missing/non-GitHub `origin`, missing upstream, non-fast-forward state, repository permission denial, and missing `gh` binary as separate conditions; authentication setup must not add or rewrite remotes, set upstreams, pull, install software, or claim it can grant repository or organization access.
4. For an authentication failure that the package can connect, show the exact external effects before requesting approval: a visible terminal may run `gh auth login`, browser/2FA/SSO remains user-controlled, `gh auth setup-git` may update the GitHub credential helper, GitHub-specific `useHttpPath` may change, and SSH remotes may run an interactive trust/authentication check. State that credentials remain owned by GitHub CLI, the OS credential manager/keychain, or the SSH agent. Include reversibility and explain that logout or Git-config rollback is a separate approved action.
5. After explicit approval, invoke only the package-owned connection flow for the exact project. Prefer the repository-approved Unity CLI against the matching Editor:

```bash
unity-cli --project "<absolute-project>" menu "Tools/Package/AI GitHub/Connect Current Project"
```

If the exact Editor or Unity CLI is unavailable, ask the user to run `Tools > Package > AI GitHub > Connect Current Project`; do not recreate the setup script or open a different project's terminal flow.
6. Wait for the user to finish browser login, 2FA, SSO, or SSH interaction. Re-run the privacy-safe diagnosis and require remote read plus push dry-run success. Report only safe classifications, the external configuration categories that changed, and any remaining permission or branch-sync action.

Never request a token in chat, read or print a token, copy credentials into project files, run a real push, add/change a remote, set an upstream, pull, modify branch history, approve organization access, or automatically log out/erase credentials. A setup invocation does not authorize those separate operations.
