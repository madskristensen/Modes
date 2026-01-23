# Visual Studio Modes

[![Build](https://github.com/madskristensen/Modes/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/Modes/actions/workflows/build.yaml)
[![Visual Studio Marketplace](https://img.shields.io/visual-studio-marketplace/v/MadsKristensen.MarkdownLintS?label=VS%20Marketplace)](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.Modes)
[![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/d/MadsKristensen.Modes)](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.Modes)

A Visual Studio extension that provides toggleable modes to quickly switch between different IDE configurations. Each mode applies a curated set of settings optimized for specific scenarios.

## Features

- **Four distinct modes** for different workflows
- **Mutually exclusive** - only one mode active at a time
- **Toggle via menu** at Tools > Modes
- **Status bar indicators** show active mode (click to toggle)
- **Baseline backup** automatically saves your settings before first mode activation
- **Session persistence** remembers active mode across VS restarts

## Modes

### 🔋 Low Power Mode

Optimizes for battery life and large solutions by disabling background work, visual effects, and performance-heavy features. Includes all Performance mode settings plus additional power-saving options.

| Setting | Value | Description |
|---------|-------|-------------|
| Animate Environment Tools | false | Disables UI animations |
| Use Hardware Graphics Acceleration | false | Reduces GPU usage |
| Auto Adjust Experience | false | Prevents auto visual adjustments |
| Reopen Documents On Solution Load | false | Faster startup |
| Restore Solution Explorer Project Hierarchy State | false | Faster solution load |
| Track File Selection In Explorer | false | Disables track active item in Solution Explorer |
| Detect File Changes Outside IDE | false | Reduces disk I/O |
| Is Background Download Enabled | false | Disables background update downloads |
| Enable Diagnostic Tools | false | Disables diagnostic tools while debugging |
| Enable Just My Code | true | Skips stepping through framework/external code |
| Code Lens | false | Disables CodeLens |
| Word Wrap | false | Disables word wrap |
| Background Analysis Scope Option | Open documents | Limits background analysis to open documents only |
| Concurrent Builds | 1 | Single-threaded builds to save CPU |
| C# Closed File Diagnostics | Disabled | No background analysis of closed files |
| Inline Parameter Name Hints | false | Reduces typing delay |
| Inline Type Hints | false | Reduces typing delay |
| Use Map Mode | false | Uses bar mode instead of map mode for scrollbar |
| Show Annotations | false | Disables scrollbar annotations |

### 🔍 Focus Mode

Minimizes distractions for deep-work coding sessions by hiding UI clutter.

| Setting | Value | Description |
|---------|-------|-------------|
| Animate Environment Tools | false | Disables UI animations |
| Auto Adjust Experience | false | Consistent visual experience |
| Show Navigation Bar | false | Hides navigation bar |
| Show Annotations | false | Hides scrollbar annotations |
| Inline Parameter Name Hints | false | Hides inline parameter hints |
| Inline Type Hints | false | Hides inline type hints |
| Fading (all) | false | Disables code fading effects |
| Show Warning Messages | false | Reduces interruptions |
| Code Lens | false | Disables CodeLens |
| Use Map Mode | false | Uses bar mode instead of map mode for scrollbar |
| Window.AutoHideAll | Executed | Auto-hides all tool windows |

### 🚀 Performance Mode

Disables features that slow down Visual Studio, cause UI hangs, typing delays, or slow solution load times.

| Setting | Value | Description |
|---------|-------|-------------|
| Reopen Documents On Solution Load | false | Faster solution load |
| Restore Solution Explorer Project Hierarchy State | false | Faster solution load |
| Track File Selection In Explorer | false | Reduces UI thread work |
| Animate Environment Tools | false | Reduces UI overhead |
| C# Closed File Diagnostics | Disabled | No background analysis of closed files |
| Skip Analyzers For Implicitly Triggered Builds | true | Faster implicit builds |
| Suggest For Types In NuGet Packages | false | Reduces network/CPU overhead |
| Inline Parameter Name Hints | false | Reduces typing delay |
| Inline Type Hints | false | Reduces typing delay |
| Use Map Mode | false | Uses bar mode instead of map mode for scrollbar |
| Show Annotations | false | Reduces scrollbar rendering |
| Concurrent Builds | 22 | Maximum parallel builds |
| Show Output Window Before Build | false | Avoids UI thread work |

### 🎤 Presenter Mode

Increases font sizes for better visibility during presentations, demos, and screen sharing.

| Setting | Value | Description |
|---------|-------|-------------|
| Text Editor Font | Cascadia Code 16pt | Larger code font |
| Statement Completion Font | Cascadia Code 14pt | Larger IntelliSense popup |
| Editor Tooltip Font | Cascadia Code 14pt | Larger tooltips |
| Environment Font | Segoe UI 11pt | Larger UI elements |
| Output Window Font | Cascadia Code 14pt | Larger output text |
| Find Results Font | Cascadia Code 14pt | Larger search results |
| Terminal Font | Cascadia Code 14pt | Larger terminal text |

## Usage

1. Open the **Tools** menu
2. Navigate to **Modes** submenu (near the bottom, above Theme)
3. Click a mode to toggle it on/off
4. Active mode shows a checkmark and displays an icon in the status bar
5. Click status bar icon to quickly toggle the mode off

## How It Works

- When you enable a mode, your current settings are exported as a baseline backup
- The mode imports its `.vssettings` file to apply the configuration
- Modes are mutually exclusive - enabling one automatically disables any other active mode
- Disabling a mode restores your baseline settings

## Contributing

Found a bug or have a feature request? Please open an issue on [GitHub](https://github.com/madskristensen/Modes/issues).

## License

This extension is open source. See the [LICENSE](LICENSE.txt) file for details.
