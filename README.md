# AI GitHub (com.actionfit.githubauth)

## 설치 (manifest.json, Git URL)

```json
{
  "dependencies": {
    "com.actionfit.githubauth": "https://github.com/ActionFit-Editor/AI_GitHub.git#1.0.11"
  }
}
```

ActionFit Unity editor automation packages에서 공통으로 사용하는 GitHub 인증 진단 패키지입니다. BuildCommit, package publish, private package 접근처럼 로컬 기기에서 `git` 명령을 실행해야 하는 기능이 GitHub 연결 상태를 확인하고 사용자에게 복구 절차를 안내할 때 사용합니다.

이 패키지는 GitHub token을 저장하지 않습니다. Windows/macOS/Linux OS 사용자 계정의 Git credential helper, GitHub CLI(`gh auth`), SSH key, 또는 이미 설정된 OS credential store/keychain을 확인하고 안내합니다.

> 보안 불변 규칙: GitHub token을 Unity package, Unity asset, `EditorPrefs`, `PlayerPrefs`, `ProjectSettings`, project/package file, 임시 setup script 또는 log에 저장·복사·출력하지 않습니다. 이 규칙은 명시적인 보안 검토와 사용자 승인 없이 수정하거나 제거하면 안 됩니다.

## Agent Skill 안내

Custom Package Manager의 `Install or Refresh Agent Skills`는 Codex와 Claude에 다음 skill을 설치합니다.

- `github-auth-help`: 로컬 인증 소유권, 실패 분류, Unity 메뉴와 token 비노출 경계를 설명합니다.
- `github-auth-setup`: 명시 호출 시 안전한 진단과 현재 연결 계획 승인을 거쳐 패키지의 visible setup terminal을 열고 다시 검증합니다.
- `github-auth-diagnose`: remote 종류, `gh` 인증 상태, remote read, push dry-run과 branch 동기화 실패를 credential 원문 없이 분류합니다.

Setup skill도 Codex 기본 컨텍스트에 포함됩니다. 컨텍스트 노출은 로그인이나 설정 변경 승인이 아니며, 로그인 terminal과 GitHub-specific Git config 영향을 먼저 설명하고 별도 승인을 받습니다. Help/diagnose skill은 raw remote URL, credential helper 출력과 token을 표시하지 않으며 `gh auth login`, Git config 변경, setup terminal 실행 또는 실제 push를 수행하지 않습니다. 어떤 skill도 token을 읽거나 프로젝트에 저장하지 않습니다.

## 제공 기능

- `GitHubAuthPreflight.CheckProjectGitHubPushAccess(projectRoot)`: 프로젝트 루트에서 `origin` remote, GitHub read access, current branch push dry-run을 확인합니다.
- `GitHubAuthPreflight.CheckAndTryConnectProject(projectRoot)`: 연결 상태를 확인하고 GitHub 인증/권한 실패일 때 현재 OS의 대화형 터미널 연결 절차를 시작한 뒤 `GitHubAuthConnectionResult`를 반환합니다.
- `GitHubAuthPreflight.CheckAndTryConnectCurrentProjectForAutomation()`: AI 또는 Unity execute-method 호출을 위한 무인자 진입점입니다. `[AIGitHubAutomation]`의 안전한 상태 필드만 Unity log에 기록합니다.
- `GitHubAuthPreflight.EnsureProjectGitHubPushAccess(projectRoot, contextName)`: 확인에 성공하면 `true`를 반환합니다. 인증/권한 실패에는 GitHub 인증 안내 팝업을, non-fast-forward 실패에는 로컬 브랜치 동기화 팝업을 띄운 뒤 `false`를 반환합니다.
- `GitHubAuthPreflight.ShowRequiredDialog(result, contextName)`: 인증 실패에는 README/AI 문의와 `연결 시도` 버튼을 표시하고, 로컬 브랜치가 뒤처진 경우에는 `git pull --ff-only` 안내를 별도로 표시합니다.
- `GitHubAuthPreflight.OpenProjectGitHubSetupTerminal(projectRoot)`: Windows PowerShell, macOS Terminal 또는 Linux terminal을 열고 현재 프로젝트 루트에서 GitHub 연결 확인/설정 스크립트를 실행합니다. 자동 실행에 실패하면 스크립트를 클립보드에 복사합니다.
- `Tools > Package > AI GitHub > Connect Current Project`: 연결 상태를 확인하고 필요할 때 OS별 대화형 연결 터미널을 엽니다.
- `Tools > Package > AI GitHub > Check Project GitHub Access`: 현재 Unity 프로젝트의 GitHub 연결을 수동으로 점검합니다.
- `Tools > Package > AI GitHub > Open Setup Terminal`: 현재 Unity 프로젝트 루트에서 GitHub 연결 시도 Terminal을 바로 엽니다.

## 로컬 GitHub 연결 확인

Unity를 실행하는 OS 사용자 계정의 터미널에서 프로젝트 루트로 이동한 뒤 아래 순서로 확인합니다.

```bash
cd <Unity project root>
git remote -v
gh auth status --hostname github.com
gh auth login --hostname github.com --git-protocol https --scopes repo,workflow
gh auth setup-git --hostname github.com
git config --global credential.https://github.com.useHttpPath false
GIT_TERMINAL_PROMPT=0 git ls-remote origin HEAD
GIT_TERMINAL_PROMPT=0 git push --dry-run
```

`git remote -v`가 `https://github.com/...`이면 `gh auth setup-git` 또는 OS credential store가 필요합니다. `git@github.com:...` 또는 `ssh://git@github.com/...`이면 GitHub에 등록된 SSH key가 필요하며 아래 명령으로 확인합니다.

## 최초 설치 자동 확인

패키지가 프로젝트에 처음 로드되면 현재 OS 사용자 기준으로 프로젝트별 1회 연결 상태를 확인합니다. `origin`이 GitHub이고 read/push dry-run 인증이 실패하면 사용자에게 보이는 OS별 터미널을 자동으로 열어 대화형 연결을 시도합니다.

- 최초 브라우저 로그인, 2FA, SSO와 조직 승인은 사용자가 직접 완료합니다.
- `origin` 누락, 비-GitHub remote, 로컬 브랜치 뒤처짐 상태에서는 연결 터미널을 자동 실행하지 않습니다.
- Unity batchmode/headless 실행에서는 대화형 터미널을 자동 실행하지 않습니다.
- `EditorPrefs`에는 project path hash가 포함된 최초 확인 완료 Boolean만 저장하며 credential이나 token은 저장하지 않습니다.

```bash
ssh -T git@github.com
GIT_TERMINAL_PROMPT=0 git ls-remote origin HEAD
GIT_TERMINAL_PROMPT=0 git push --dry-run
```

## Unity Tool에서 연결 시도

`Connect Current Project`를 실행하거나 `Check Project GitHub Access` 실패 팝업에서 `연결 시도`를 누르면 현재 OS의 터미널을 열고 아래 작업을 순서대로 실행합니다.

- `origin` remote 확인
- HTTPS remote일 때 `gh auth status`, 필요 시 `gh auth login --hostname github.com --git-protocol https --scopes repo,workflow`
- `gh auth setup-git --hostname github.com`
- HTTPS remote일 때 `credential.https://github.com.useHttpPath=false`를 설정해 같은 GitHub 계정을 여러 repository에서 재사용
- SSH remote일 때 `ssh -T git@github.com`
- `GIT_TERMINAL_PROMPT=0 git ls-remote origin HEAD`
- `GIT_TERMINAL_PROMPT=0 git push --dry-run`

이 기능은 GitHub token을 Unity 프로젝트나 EditorPrefs에 저장하지 않습니다. 실제 인증은 GitHub CLI, OS credential store/keychain, Git credential helper, SSH key 같은 로컬 계정 설정에 위임합니다.

## 오류별 안내

- `fatal: could not read Username for 'https://github.com': Device not configured`
  - Unity가 실행한 `git push`가 GitHub credential prompt를 열 수 없거나 credential helper를 찾지 못한 상태입니다.
  - Unity를 실행하는 같은 OS 사용자 계정의 터미널에서 `gh auth login`, `gh auth setup-git`을 완료한 뒤 다시 확인합니다.
- `Permission denied (publickey)`
  - SSH remote를 쓰고 있지만 현재 사용자에게 GitHub SSH key가 없거나 등록되지 않은 상태입니다.
  - `ssh -T git@github.com`으로 확인하고 GitHub 계정에 public key를 등록합니다.
- `Repository not found` 또는 `403`
  - 인증은 되었지만 해당 계정에 repository read/write 권한이 없을 수 있습니다.
  - GitHub organization/repository 권한과 fine-grained token approval 상태를 확인합니다.
- `The current branch ... has no upstream branch`
  - BuildCommit은 로컬 `git push`를 사용하므로 현재 브랜치 upstream이 필요합니다.
  - 일반적으로 `git push -u origin <branch>`로 upstream을 먼저 설정합니다.
- `non-fast-forward`, `fetch first`, 또는 `tip of your current branch is behind its remote counterpart`
  - GitHub 연결은 성공했지만 현재 로컬 브랜치가 원격 브랜치보다 뒤처진 상태입니다. 인증 오류가 아닙니다.
  - `git status --short --branch`로 로컬 변경을 확인한 뒤 `git pull --ff-only`로 원격 변경을 통합하고 BuildCommit을 다시 실행합니다.

## BuildAutomation 연동

`com.actionfit.buildautomation`은 `Commit, Tag & Push` 실행 전에 이 패키지의 preflight를 호출합니다. 인증 또는 브랜치 동기화 확인이 실패하면 커밋과 태그를 만들기 전에 중단합니다. 인증 실패에는 GitHub 인증 필요 팝업을, 로컬 브랜치가 뒤처진 경우에는 로컬 브랜치 동기화 필요 팝업을 표시합니다.

팝업이 표시되면 `연결 시도` 버튼을 누르거나, 이 README의 로컬 GitHub 연결 확인 절차를 따르거나, AI에게 "BuildCommit GitHub 인증 가이드 알려줘"라고 문의하면 됩니다.

## Unity 메뉴

- 패키지 root: `Tools > Package > AI GitHub`
- 연결: `Tools > Package > AI GitHub > Connect Current Project`
- README: `Tools > Package > AI GitHub > README`
- 패키지 명령은 같은 package root 아래에 유지하며 README/Setting SO 항목이 있으면 분리된 해당 항목보다 위에 표시합니다.
