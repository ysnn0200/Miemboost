# UI Plan

## Visual Direction

Miemboost should use a macOS-inspired Windows desktop style:

- Dark translucent shell.
- Soft sidebar.
- Minimal controls.
- Clear system state cards.
- Restrained status colors.
- No neon-heavy "gaming booster" look.
- No fake exaggerated numbers.

## Main Navigation

- Overview.
- Game Library.
- One-click Boost.
- Network Diagnostics.
- Process Management.
- Optimization History.
- Settings.

## Overview

The overview is a state dashboard:

- Current mode: Conservative, Balanced, Aggressive.
- Detected game.
- Primary Boost button.
- CPU, GPU, memory, and network cards.
- Recent optimization records.
- Paused apps with Restore button.

## One-click Boost

This page must show exactly what will happen before execution:

- Switch power plan.
- Lower approved background process priority.
- Release Standby Memory.
- Pause approved background apps.
- Run DNS/Ping diagnostics.

Each row should show:

- Status.
- Risk level.
- Whether restore is available.
- Details drawer.

## Game Library

Each game can have a profile:

- Executable path.
- Recommended mode.
- Launch tracking.
- Auto-restore on exit.
- Allowed background apps to pause.
- Network diagnostic targets.

## Network Diagnostics

This page is diagnostic first:

- Current adapter: Ethernet or Wi-Fi.
- Ping.
- Jitter.
- Packet loss.
- DNS response.
- Network-heavy background processes.
- Recommended actions.

It must not present opaque proxy or packet modification as "optimization".

## Game-time Behavior

When a game is running:

- Reduce telemetry refresh rate.
- Pause decorative animation.
- Minimize to tray if enabled.
- Keep restore available.
- Avoid scanning large process lists repeatedly.

