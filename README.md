# GitHub Auth

ActionFit Unity editor automation packages에서 공통으로 사용하는 GitHub 인증 진단 패키지입니다. BuildCommit, package publish, private package 접근처럼 로컬 기기에서 `git` 명령을 실행해야 하는 기능이 GitHub 연결 상태를 확인하고 사용자에게 복구 절차를 안내할 때 사용합니다.

이 패키지는 GitHub token을 저장하지 않습니다. 로컬 macOS 사용자 계정의 Git credential helper, GitHub CLI(`gh auth`), SSH key, 또는 이미 설정된 OS keychain credential을 확인하고 안내합니다.

## 제공 기능

- `GitHubAuthPreflight.CheckProjectGitHubPushAccess(projectRoot)`: 프로젝트 루트에서 `origin` remote, GitHub read access, current branch push dry-run을 확인합니다.
- `GitHubAuthPreflight.EnsureProjectGitHubPushAccess(projectRoot, contextName)`: 인증 확인에 성공하면 `true`를 반환하고, 실패하면 GitHub 인증 안내 팝업을 띄운 뒤 `false`를 반환합니다.
- `GitHubAuthPreflight.ShowRequiredDialog(result, contextName)`: 인증 실패 시 Unity 팝업으로 README/AI 문의 안내와 기본 명령 시퀀스를 보여줍니다.
- `Tools > ActionFit > GitHub Auth > Check Project GitHub Access`: 현재 Unity 프로젝트의 GitHub 연결을 수동으로 점검합니다.

## 로컬 GitHub 연결 확인

Unity를 실행하는 macOS 사용자 계정의 터미널에서 프로젝트 루트로 이동한 뒤 아래 순서로 확인합니다.

```bash
cd <Unity project root>
git remote -v
gh auth status --hostname github.com
gh auth login --hostname github.com --git-protocol https --scopes repo,workflow
gh auth setup-git --hostname github.com
GIT_TERMINAL_PROMPT=0 git ls-remote origin HEAD
GIT_TERMINAL_PROMPT=0 git push --dry-run
```

`git remote -v`가 `https://github.com/...`이면 `gh auth setup-git` 또는 macOS keychain credential이 필요합니다. `git@github.com:...` 또는 `ssh://git@github.com/...`이면 GitHub에 등록된 SSH key가 필요하며 아래 명령으로 확인합니다.

```bash
ssh -T git@github.com
GIT_TERMINAL_PROMPT=0 git ls-remote origin HEAD
GIT_TERMINAL_PROMPT=0 git push --dry-run
```

## 오류별 안내

- `fatal: could not read Username for 'https://github.com': Device not configured`
  - Unity가 실행한 `git push`가 GitHub credential prompt를 열 수 없거나 credential helper를 찾지 못한 상태입니다.
  - 같은 macOS 사용자 계정의 터미널에서 `gh auth login`, `gh auth setup-git`을 완료한 뒤 Unity를 다시 실행합니다.
- `Permission denied (publickey)`
  - SSH remote를 쓰고 있지만 현재 사용자에게 GitHub SSH key가 없거나 등록되지 않은 상태입니다.
  - `ssh -T git@github.com`으로 확인하고 GitHub 계정에 public key를 등록합니다.
- `Repository not found` 또는 `403`
  - 인증은 되었지만 해당 계정에 repository read/write 권한이 없을 수 있습니다.
  - GitHub organization/repository 권한과 fine-grained token approval 상태를 확인합니다.
- `The current branch ... has no upstream branch`
  - BuildCommit은 로컬 `git push`를 사용하므로 현재 브랜치 upstream이 필요합니다.
  - 일반적으로 `git push -u origin <branch>`로 upstream을 먼저 설정합니다.

## BuildAutomation 연동

`com.actionfit.buildautomation`은 `Commit, Tag & Push` 실행 전에 이 패키지의 preflight를 호출합니다. 인증이 실패하면 커밋과 태그를 만들기 전에 중단하고, Unity 팝업에 GitHub 인증 필요 문구와 README/AI 문의 안내를 표시합니다.

팝업이 표시되면 이 README의 로컬 GitHub 연결 확인 절차를 따르거나, AI에게 "BuildCommit GitHub 인증 가이드 알려줘"라고 문의하면 됩니다.
