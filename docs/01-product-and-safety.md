# Product And Safety

## Name

The product name is **Miemboost**.

## Positioning

Miemboost is a quiet, transparent, reversible Windows game performance assistant. It should feel closer to a macOS system utility than an "esports cleaner" or cheat-adjacent tool.

## Expected Resource Usage

The production target depends on the final UI framework, but the design budget is:

- Idle UI open: 80 MB to 180 MB.
- Boost execution window: 150 MB to 300 MB.
- Optional tray/helper process: 20 MB to 80 MB.
- Idle CPU: approximately 0%.
- Game-time monitoring: 0.2% to 2% CPU.

To avoid negative optimization, the UI should reduce animations, slow telemetry refresh, and move into tray mode when a game is running.

## Explicitly Forbidden Features

Miemboost must not implement:

- Game process injection.
- FPS overlay injection or DirectX/Vulkan/OpenGL hooking.
- Game memory scanning, reading, or writing.
- Game packet interception or modification.
- Anti-cheat service disabling or interference.
- Macro, rapid-fire, recoil, input automation, or script features.
- Game config changes that bypass intended limits.
- Opaque VPN, virtual adapter, or proxy behavior that hides what is happening.

These features increase ban risk and can make the product look like cheat tooling.

## Allowed Optimization Surface

Miemboost may implement:

- Power plan switching with restore.
- Game process priority changes.
- Non-critical process priority lowering.
- User-approved background application pause/close.
- Standby Memory release as a manual or profile-controlled action.
- DNS cache flush and DNS performance diagnostics.
- Ping, jitter, packet loss, and background network usage diagnostics.
- Startup/background app visibility.
- Per-game optimization profiles.

## Risk Model

Each operation must be tagged with a risk level:

- Safe: read-only diagnostics or reversible low-impact system changes.
- Balanced: user-approved process pause, priority adjustment, Standby Memory release.
- Aggressive: service/task pause or system-wide changes that need stronger warnings.

The first version should ship Safe and Balanced features only. Aggressive actions can be designed but should remain disabled until tested carefully.

