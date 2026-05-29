# Architecture

## Design Goal

The codebase should separate UI, business decisions, and Windows-specific implementation. This keeps the app testable and prevents unsafe optimization logic from being hidden inside button handlers.

## Planned Projects

```text
src/Miemboost.App
  Desktop shell, views, view models, styling, tray integration.

src/Miemboost.Core
  Domain models, optimization plans, risk levels, snapshots, profile rules.

src/Miemboost.Windows
  Windows adapters for processes, power plans, memory, DNS, network diagnostics.

src/Miemboost.Tests
  Unit tests for planning, risk classification, snapshots, and restore logic.
```

## Core Concepts

### OptimizationPlan

A previewable list of actions generated from:

- Selected game.
- Selected mode: Conservative, Balanced, Aggressive.
- Per-game profile.
- Current diagnostics.
- Safety policy.

Plans are data first. The UI renders a plan before execution, and the executor only runs approved actions.

### OptimizationAction

Each action must expose:

- `Id`
- `Title`
- `Description`
- `RiskLevel`
- `RequiresElevation`
- `CanRestore`
- `Preview`
- `Execute`
- `Restore`

Actions that cannot be restored must be rare and clearly marked.

### SystemSnapshot

Before Boost, Miemboost records only what it needs to restore:

- Previous power plan.
- Process priorities changed by Miemboost.
- Apps paused or closed by Miemboost.
- DNS/network settings touched by Miemboost.
- Timestamp, game profile, and app version.

Snapshots should be stored locally as structured JSON and never include private game data.

### SafetyPolicy

The safety policy blocks dangerous operations centrally. UI code should not decide whether an action is allowed.

Examples:

- Block actions that target protected system processes.
- Block actions that target known anti-cheat processes or services.
- Block actions that request injection, hooks, packet modification, or input automation.
- Block aggressive actions unless the user explicitly enables that mode.

## Execution Flow

1. Detect or select game.
2. Run lightweight diagnostics.
3. Generate `OptimizationPlan`.
4. Show plan in UI.
5. User approves.
6. Save `SystemSnapshot`.
7. Execute actions sequentially with progress.
8. Write optimization record.
9. Reduce UI/monitoring overhead while game runs.
10. Restore automatically on game exit if the profile allows it.

## Error Handling

- A failed action must not stop restore from being available.
- Partial execution must be recorded.
- Restore should be idempotent: running it twice should be safe.
- Every elevated action should have a clear non-elevated fallback or explanation.

