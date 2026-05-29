# Git Policy

## Local Repository

This project uses a dedicated git repository at:

```text
D:\代码\Miemboost
```

The parent directory is not part of this repository.

## Commit Discipline

Every meaningful change must be committed before moving to the next phase.

Recommended commit style:

```text
docs: define product safety baseline
feat: add optimization plan model
feat: add safe process scanner
test: cover restore snapshot behavior
ui: build overview dashboard shell
```

## Branching

Until a remote is provided, work can stay on `main`. After a remote exists:

- `main`: stable baseline.
- `feature/*`: individual features.
- `risk/*`: experiments that may affect system behavior.

## Remote Push

No remote repository is configured yet. After a GitHub/Gitee/GitLab remote URL is provided, commits should be pushed after each completed phase.

## Pre-commit Checklist

- `git status --short` reviewed.
- Only intended files changed.
- Risk boundaries still respected.
- Any executable behavior has tests or a manual verification note.
- Documentation updated if behavior changed.

