# AI Guide - AI GitHub

This file is shipped inside the UPM package so an AI assistant in a consuming Unity project can understand the package without access to the source project's `Docs/AI` folder.

## Package Identity

- Package ID: `com.actionfit.githubauth`
- Display name: AI GitHub
- Repository: `https://github.com/ActionFit-Editor/AI_GitHub.git`
- Current package version at generation time: `1.0.7`
- Unity version: `6000.2`

## Purpose

AI GitHub owns shared GitHub authentication diagnostics and guidance for ActionFit Unity editor automation packages. Use it when an editor tool must verify local GitHub access before running `git push`, tag push, private repository reads, or package publish operations.

This package does not store GitHub tokens. It checks and explains the local environment: Git remotes, GitHub CLI auth, credential helpers, SSH key access, `git ls-remote`, and `git push --dry-run`.

## Protected Security Invariant

- Never store, copy, serialize, cache, print, or commit a GitHub token in Unity packages, Unity assets, `EditorPrefs`, `PlayerPrefs`, `ProjectSettings`, package files, project files, temporary setup scripts, or logs.
- Authentication secrets must remain owned by GitHub CLI, the operating-system credential manager/keychain, or the user's SSH agent and key files.
- `EditorPrefs` may store only a non-secret Boolean indicating that a project-scoped first-install check was attempted.
- Do not weaken, remove, bypass, or reinterpret this invariant without an explicit security review and explicit user approval for the exact credential storage design.
- AI agents must refuse package changes that silently persist tokens or expose credential-helper output containing secret values.

## Project Router Registration

This package should be listed in `Packages/com.actionfit.custompackagemanager/PACKAGE_AI_GUIDE_ROUTER.md`.

Requested router entry:

- `Packages/com.actionfit.githubauth/AI_GUIDE.md` - AI GitHub provides shared GitHub credential diagnostics and user guidance for ActionFit Unity editor automation packages. Read when changing local GitHub authentication checks, push preflight behavior, token/credential guidance, or packages that depend on GitHub push/publish access.

## Required Reading For AI

- Read this `AI_GUIDE.md` before changing, diagnosing, or explaining this package.
- Read `README.md` for user-facing setup, command sequences, and troubleshooting text.
- Read `package.json` for package ID, version, Unity version, and dependencies.
- Read `Editor/PackageInfo/ActionFitPackageInfo_SO.asset` for catalog metadata, repository name, description, and release note.

## Editing Rules

- Keep reusable GitHub credential diagnostics in this package instead of duplicating command sequences across other packages.
- The Protected Security Invariant is mandatory. Do not modify or remove it as part of ordinary maintenance, feature work, refactoring, or release preparation.
- Do not log raw tokens, credential helper output that may contain secrets, or complete environment dumps.
- Keep user-facing failure guidance actionable and short enough to fit a Unity dialog.
- The setup-terminal helper may run `gh auth`/SSH checks in the user's visible terminal. It may configure Git's GitHub credential helper and GitHub-specific `useHttpPath`, but must never read or persist the token itself.
- When dependent packages such as Build Automation need local GitHub access, call `GitHubAuthPreflight` and reference this README instead of embedding a second full guide.
- When behavior changes, update this `AI_GUIDE.md`, `README.md`, and PackageInfo release notes before publishing.

## Behavior Notes

- Menus: `Tools/Package/AI GitHub/Connect Current Project`, `Tools/Package/AI GitHub/Check Project GitHub Access`, and `Tools/Package/AI GitHub/Open Setup Terminal`.
- Main API: `ActionFit.GitHubAuth.Editor.GitHubAuthPreflight.CheckProjectGitHubPushAccess(string projectRoot)`.
- AI/automation API: `GitHubAuthPreflight.CheckAndTryConnectProject(string projectRoot)` returns `GitHubAuthConnectionResult`. `CheckAndTryConnectCurrentProjectForAutomation()` is a no-argument Unity execute-method entry point and emits only the safe `[AIGitHubAutomation]` status fields.
- Reusable guard API: `ActionFit.GitHubAuth.Editor.GitHubAuthPreflight.EnsureProjectGitHubPushAccess(string projectRoot, string contextName)` returns `true` when local GitHub read/push checks pass. Authentication and permission failures show the shared GitHub authentication dialog, while a non-fast-forward push rejection shows a separate local-branch synchronization dialog. Both failures return `false`.
- Dialog API: `ActionFit.GitHubAuth.Editor.GitHubAuthPreflight.ShowRequiredDialog(GitHubAuthCheckResult result, string contextName)` and the overload with `projectRoot`. The failure dialog includes a `연결 시도` button that opens the setup terminal.
- Setup Terminal API: `ActionFit.GitHubAuth.Editor.GitHubAuthPreflight.OpenProjectGitHubSetupTerminal(string projectRoot)` writes a temporary Windows PowerShell, macOS `.command`, or Linux `.sh` setup script and opens it in a visible terminal. Launch failures copy the script to the clipboard instead.
- First-install bootstrap runs once per project and OS user. It checks access after editor package load and starts an interactive terminal only for a GitHub remote authentication/permission failure. It does not auto-launch for missing/non-GitHub origins, branch synchronization failures, or batchmode.
- HTTPS setup runs `gh auth login` only when needed, calls `gh auth setup-git --hostname github.com`, and sets `credential.https://github.com.useHttpPath=false` so the same GitHub account can be reused across package repositories.
- The push preflight uses `GIT_TERMINAL_PROMPT=0` so Unity does not hang waiting for terminal credential prompts.
- The standard check sequence is:
  1. `git remote get-url origin`
  2. reject non-GitHub origins for GitHub-specific automation
  3. `git ls-remote origin HEAD`
  4. `git push --dry-run`
- If HTTPS auth fails with `could not read Username`, guide the user to run `gh auth login --hostname github.com --git-protocol https --scopes repo,workflow`, then `gh auth setup-git --hostname github.com`, then retry `GIT_TERMINAL_PROMPT=0 git push --dry-run`.
- If SSH auth fails with `Permission denied (publickey)`, guide the user to verify `ssh -T git@github.com` and register a public key with GitHub.
- If push dry-run fails with `non-fast-forward`, `fetch first`, or the local-tip-behind message, classify it as `GitHubAuthCheckFailureKind.BranchOutOfDate`. Do not present it as an authentication failure; guide the user to inspect local changes and run `git pull --ff-only`.
- Build Automation depends on this package and uses it before BuildCommit creates the request commit/tag.

## Package Tools Menu

- Unity menu root: `Tools/Package/AI GitHub/`.
- Keep package commands under this package root.
- Lower separated entries:
- `README`: opens this package README.
- Do not add README or Setting SO access back to Custom Package Manager package rows or Project Files.

## Release Note Rules

- `ActionFitPackageInfo_SO.ReleaseNote` must contain only the single version being prepared.
- Do not copy older changelog entries into the newest release note.
- Version history and update-range summaries are composed by Custom Package Manager from separate catalog version rows.

## Publish Notes

- Publishing is manual through Custom Package Manager.
- Do not manually add `com.actionfit.githubauth` catalog rows before the package is actually published.
- Keep using the existing `AI_GitHub` repository; do not let the publisher create a second repository for the same package ID under the legacy `GitHub_Auth` name.
- Before reusing a version, check the remote Git tags. Published tags are immutable.
