# Implementation Roadmap

## Phase 1: Core Safety Model

Status: started.

Deliverables:

- Risk levels.
- Optimization action descriptors.
- Optimization plan builder.
- Safety policy.
- Snapshot model.
- Unit tests for forbidden and allowed boundaries.

No Windows system changes are executed in this phase.

## Phase 2: Windows Read-only Diagnostics

Status: in progress.

Deliverables:

- Process list reader. Started.
- CPU and memory summary. Started.
- Network adapter summary. Started.
- Ping, jitter, and packet loss probe. Started.
- DNS response probe. Started.

Rules:

- Read-only only.
- No elevated execution.
- No game process memory access.
- No anti-cheat process inspection beyond name/path exclusion.

## Phase 3: Reversible Actions

Deliverables:

- Power plan switch and restore.
- Process priority change and restore.
- User-approved background app pause/close with restore record.
- Optimization history.

Rules:

- Snapshot is mandatory before execution.
- Restore must be idempotent.
- Anti-cheat and protected processes must be blocked centrally.

## Phase 4: Desktop Shell

Deliverables:

- macOS-inspired shell.
- Sidebar navigation.
- Overview dashboard.
- Boost preview page.
- Restore surface.
- Game library profile editor.
- Settings.

Rules:

- UI must preview plan data from Core instead of hardcoding actions.
- Game-time UI refresh must slow down.
- Decorative animation must pause during game sessions.

## Phase 5: Controlled Memory And Network Utilities

Deliverables:

- Standby Memory release as an explicit pre-game action.
- DNS cache flush.
- Safer network recommendations.

Rules:

- No packet interception or modification.
- No opaque proxy/VPN features in MVP.
- No repeated in-game memory clearing loop.

## Phase 6: Packaging And Trust

Deliverables:

- Installer.
- Code signing plan.
- Privacy note.
- Safety FAQ.
- Crash/error logs without private game data.

Rules:

- No silent elevation.
- No hidden background persistence.
- Clear uninstall and restore behavior.
