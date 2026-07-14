---
name: github-auth-diagnose
description: Diagnose local GitHub remote, CLI authentication, read access, push dry-run, and branch-state failures without printing credentials or changing configuration.
---

# Diagnose GitHub Access Safely

Keep the diagnostic read-only and privacy-safe. Never print raw remote URLs, credential-helper output, tokens, environment dumps, SSH private-key paths, or captured Git error text.

1. Read repository instructions plus the AI GitHub `README.md` and `AI_GUIDE.md`.
2. Confirm the selected repository root and current branch with read-only Git commands. Do not fetch, checkout, pull, set upstream, or change Git configuration.
3. Capture `git remote get-url origin` without echoing it. Report only one classification: `github-https`, `github-ssh`, `missing`, or `non-github`. If the URL contains userinfo before `@`, do not reproduce any portion of it.
4. Check GitHub CLI authentication only by exit status:

```bash
if gh auth status --hostname github.com >/dev/null 2>&1; then
  printf '%s\n' 'gh_auth=ok'
else
  printf '%s\n' 'gh_auth=unavailable-or-not-authenticated'
fi
```

Do not add `--show-token` and do not copy the normal `gh auth status` output into the report.

5. Check remote read access with `GIT_TERMINAL_PROMPT=0 git ls-remote origin HEAD`, suppressing command output and reporting only success/failure.
6. Capture `GIT_TERMINAL_PROMPT=0 git push --dry-run` output in memory without echoing it. Classify only the result:
   - `ok`;
   - `missing-upstream`;
   - `branch-out-of-date` for non-fast-forward/fetch-first/tip-behind messages;
   - `authentication` for credential or public-key failures;
   - `permission` for repository-not-found/403/denied failures;
   - `unknown` for anything else.
7. Immediately discard captured output and report only the command exit codes, safe classifications, and the matching recovery guidance from the installed README.

Do not run `gh auth login`, `gh auth setup-git`, `git config`, `ssh-add`, `git pull`, `git push`, or any Unity connect/setup menu. A dry-run must remain a dry-run.
