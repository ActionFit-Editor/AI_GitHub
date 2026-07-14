---
name: github-auth-help
description: Explain AI GitHub authentication diagnostics, installed skills, Unity menus, local credential ownership, failure classes, and token non-disclosure boundaries.
---

# AI GitHub Help

Answer in the user's language. Explain authentication diagnostics without starting login, changing credential helpers, opening setup terminals, or exposing account secrets unless the user separately requests an appropriate setup workflow.

1. Read `PACKAGE_SKILLS.md` first. Treat its generated package identity, complete related-skill table, `$skill-name` invocations, descriptions, and access boundaries as authoritative.
2. Read `Packages/com.actionfit.githubauth/README.md` and `Packages/com.actionfit.githubauth/AI_GUIDE.md` when present. If downloaded, resolve `Library/PackageCache/com.actionfit.githubauth@*` without editing it.
3. Explain that `$github-auth-diagnose` checks only safe status boundaries: GitHub remote kind, GitHub CLI availability/auth status, remote read access, push dry-run, and branch synchronization classification.
4. Explain the distinct failure classes: missing/non-GitHub origin, missing CLI authentication, HTTPS authentication, SSH key authentication, repository permission, missing upstream, and non-fast-forward branch state.
5. State that this package never stores, copies, serializes, caches, or prints tokens. Authentication remains owned by GitHub CLI, the operating-system credential manager/keychain, or the user's SSH agent and key files.

List `Connect Current Project`, `Check Project GitHub Access`, `Open Setup Terminal`, and `README` under `Tools > Package > AI GitHub`. These Unity menu actions may open an interactive setup flow; explaining them does not authorize running them.
