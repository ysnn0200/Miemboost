# Miemboost

Miemboost is a conservative Windows game performance assistant with a macOS-inspired UI.

The project goal is not to behave like a game cheat, packet modifier, injector, or opaque "magic booster". Miemboost should only perform system-level, explainable, reversible optimizations.

## Product Principles

- Safe by default.
- Every optimization is visible before execution.
- Every change that can affect the system has a restore path.
- No game process injection, memory scanning, render hook, packet modification, macro, or anti-cheat interference.
- Game-time overhead must stay low: no high-frequency polling, no heavy animation while a game is running, and no unnecessary background work.

## Planned Stack

Preferred production stack:

- .NET 8 or later
- WPF or WinUI 3 shell
- MVVM architecture
- Windows service/helper process only if a feature truly requires elevation or background execution

Current repository status:

- The local machine does not have the .NET SDK installed yet.
- This first baseline therefore defines architecture, risk boundaries, UI direction, and git discipline before implementation.

## Repository Layout

```text
Miemboost/
  docs/
    01-product-and-safety.md
    02-architecture.md
    03-ui-plan.md
    04-git-policy.md
  src/
    Miemboost.App/          # Desktop UI shell, planned.
    Miemboost.Core/         # Domain models and orchestration, planned.
    Miemboost.Windows/      # Windows-specific adapters, planned.
    Miemboost.Tests/        # Unit tests, planned.
```

## First MVP

- Game library management.
- One-click Boost preview.
- One-click Restore.
- Safe process analysis.
- User-approved background app pause list.
- Power plan switching with snapshot restore.
- Standby Memory release as an explicit action.
- Game process priority adjustment.
- Ping, jitter, loss, DNS diagnostics.
- Per-game profile.

## Build And Run

```powershell
D:\代码\.dotnet\dotnet.exe build Miemboost.sln --configuration Release
D:\代码\.dotnet\dotnet.exe run --project src\Miemboost.App\Miemboost.App.csproj --configuration Release
```

## Publish

```powershell
.\scripts\publish-windows.ps1
```

The portable output is written to `artifacts\publish\Miemboost-win-x64`.
