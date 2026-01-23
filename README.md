# Visual Studio Modes

[![Build](https://github.com/madskristensen/Modes/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/Modes/actions/workflows/build.yaml)
[![Visual Studio Marketplace](https://img.shields.io/visual-studio-marketplace/v/MadsKristensen.Modes?label=VS%20Marketplace)](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.Modes)
[![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/d/MadsKristensen.Modes)](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.Modes)

A Visual Studio extension that provides toggleable modes to quickly switch between different IDE configurations. Each mode applies a curated set of settings optimized for specific scenarios.

![Menu](art/menu.png)

## Features

- **Four distinct modes** for different workflows
- **Toggle via menu** at Tools > Modes
- **Status bar indicator** shows active mode (click to toggle off)
- **Baseline backup** automatically saves your settings before first mode activation
- **Session persistence** remembers active mode across VS restarts
- **Auto Low Power mode** automatically enables when Windows enters battery saver mode
- **Auto-backup settings** periodically backs up your VS settings when idle
- **Restore Settings dialog** to restore from automatic backups or create new ones

## Usage

1. Open the **Tools** menu
2. Navigate to **Modes** submenu
3. Click a mode to toggle it on/off
4. Active mode shows a checkmark and displays a flag indicator in the status bar
5. Click the status bar indicator to quickly toggle the mode off

![Statusbar](art/statusbar.png)

## Options

Access options via **Tools > Options > Modes > General**:

![Settings](art/settings.png)

| Option | Default | Description |
|--------|---------|-------------|
| Auto-backup settings | true | Automatically back up Visual Studio settings when the computer is idle. |
| Auto-enable Low Power mode | true | Automatically enable Low Power mode when Windows enters power saver/battery saver mode. |
| Backup interval (hours) | 48 | Minimum time between automatic backups in hours. |

## Restore Settings

Use **Tools > Modes > Restore Settings...** to restore from a previous backup or create a new backup on demand.

![Restore settings](art/restore.png)

## Modes

### 🔋 Low Power Mode

Optimizes for battery life and large solutions by disabling background work, visual effects, and performance-heavy features.

<!-- TODO: Add screenshot of Low Power mode active -->

| Setting | Value | Description |
|---------|-------|-------------|
| Animate Environment Tools | false | Disables UI animations |
| Auto Adjust Experience | false | Prevents auto visual adjustments |
| Background Analysis Scope Option | Open documents | Limits background analysis to open documents only |
| C# Closed File Diagnostics | Disabled | No background analysis of closed files |
| Code Lens | false | Disables CodeLens |
| Concurrent Builds | 1 | Single-threaded builds to save CPU |
| Detect File Changes Outside IDE | false | Reduces disk I/O |
| Enable Diagnostic Tools | false | Disables diagnostic tools while debugging |
| Enable Just My Code | true | Skips stepping through framework/external code |
| Inline Parameter Name Hints | false | Reduces typing delay |
| Inline Type Hints | false | Reduces typing delay |
| Is Background Download Enabled | false | Disables background update downloads |
| Reopen Documents On Solution Load | false | Faster startup |
| Restore Solution Explorer Project Hierarchy State | false | Faster solution load |
| Show Annotations | false | Disables scrollbar annotations |
| Track File Selection In Explorer | false | Disables track active item in Solution Explorer |
| Use Hardware Graphics Acceleration | false | Reduces GPU usage |
| Use Map Mode | false | Uses bar mode instead of map mode for scrollbar |
| Word Wrap | false | Disables word wrap |

### 🔍 Focus Mode

Minimizes distractions for deep-work coding sessions by hiding UI clutter and auto-hiding tool windows.

| Setting | Value | Description |
|---------|-------|-------------|
| Animate Environment Tools | false | Disables UI animations |
| Auto Adjust Experience | false | Consistent visual experience |
| Code Lens | false | Disables CodeLens |
| Fading (all) | false | Disables code fading effects |
| Inline Parameter Name Hints | false | Hides inline parameter hints |
| Inline Type Hints | false | Hides inline type hints |
| Show Annotations | false | Hides scrollbar annotations |
| Show Navigation Bar | false | Hides navigation bar |
| Show Warning Messages | false | Reduces interruptions |
| Use Map Mode | false | Uses bar mode instead of map mode for scrollbar |
| Window.AutoHideAll | Executed | Auto-hides all tool windows |

### 🚀 Performance Mode

Disables features that slow down Visual Studio, cause UI hangs, typing delays, or slow solution load times.

| Setting | Value | Description |
|---------|-------|-------------|
| Animate Environment Tools | false | Reduces UI overhead |
| C# Closed File Diagnostics | Disabled | No background analysis of closed files |
| Concurrent Builds | 22 | Maximum parallel builds |
| Inline Parameter Name Hints | false | Reduces typing delay |
| Inline Type Hints | false | Reduces typing delay |
| Reopen Documents On Solution Load | false | Faster solution load |
| Restore Solution Explorer Project Hierarchy State | false | Faster solution load |
| Show Annotations | false | Reduces scrollbar rendering |
| Show Output Window Before Build | false | Avoids UI thread work |
| Skip Analyzers For Implicitly Triggered Builds | true | Faster implicit builds |
| Suggest For Types In NuGet Packages | false | Reduces network/CPU overhead |
| Track File Selection In Explorer | false | Reduces UI thread work |
| Use Map Mode | false | Uses bar mode instead of map mode for scrollbar |

### 🎤 Presenter Mode

Increases font sizes for better visibility during presentations, demos, and screen sharing.

| Setting | Value | Description |
|---------|-------|-------------|
| Editor Tooltip Font | Cascadia Code 14pt | Larger tooltips |
| Environment Font | Segoe UI 11pt | Larger UI elements |
| Find Results Font | Cascadia Code 14pt | Larger search results |
| Output Window Font | Cascadia Code 14pt | Larger output text |
| Statement Completion Font | Cascadia Code 14pt | Larger IntelliSense popup |
| Terminal Font | Cascadia Code 14pt | Larger terminal text |
| Text Editor Font | Cascadia Code 16pt | Larger code font |

## How It Works

- When you enable a mode, your current settings are exported as a baseline backup
- The mode's `.vssettings` file is imported to apply the configuration
- Modes are mutually exclusive - enabling one automatically disables any other active mode
- Disabling a mode restores your baseline settings
- The active mode persists across Visual Studio restarts

## Contributing

Found a bug or have a feature request? Please open an issue on [GitHub](https://github.com/madskristensen/Modes/issues).
