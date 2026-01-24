using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Modes
{
    /// <summary>
    /// Manages mode states, settings application, and status bar indicators.
    /// </summary>
    internal sealed class ModeManager
    {
        private static ModeManager _instance;
        private static readonly object _lock = new object();

        private ModeType? _activeMode;
        private FrameworkElement _statusBarIndicator;
        private readonly Dictionary<ModeType, string> _modeSettingsFiles;
        private readonly string _baselineBackupPath;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static ModeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ModeManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private ModeManager()
        {
            var extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var settingsDir = Path.Combine(extensionDir, "Settings");

            _modeSettingsFiles = new Dictionary<ModeType, string>
            {
                { ModeType.LowPower, Path.Combine(settingsDir, "LowPower.vssettings") },
                { ModeType.Focus, Path.Combine(settingsDir, "Focus.vssettings") },
                { ModeType.Performance, Path.Combine(settingsDir, "Performance.vssettings") },
                { ModeType.Presenter, Path.Combine(settingsDir, "Presenter.vssettings") }
            };

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baselineBackupPath = Path.Combine(appDataPath, Constants.Storage.AppDataFolderName, Constants.Storage.BaselineFileName);
        }

        /// <summary>
        /// Gets whether a specific mode is active.
        /// </summary>
        public bool IsModeActive(ModeType mode) => _activeMode == mode;

        /// <summary>
        /// Gets the currently active mode, if any.
        /// </summary>
        public ModeType? ActiveMode => _activeMode;

        /// <summary>
        /// Initializes the manager and loads persisted mode states.
        /// </summary>
        public async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Load persisted active mode from options
                General options = await General.GetLiveInstanceAsync();
                if (!string.IsNullOrEmpty(options.ActiveModeName) && Enum.TryParse(options.ActiveModeName, out ModeType mode))
                {
                    _activeMode = mode;
                }

                // Apply active mode and update UI
                if (_activeMode.HasValue)
                {
                    await ApplyActiveModeAsync();
                    await UpdateStatusBarIndicatorAsync();
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Toggles a mode on or off. Modes are mutually exclusive - enabling one disables others.
        /// </summary>
        public async Task ToggleModeAsync(ModeType mode)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var isCurrentlyActive = _activeMode == mode;

                if (isCurrentlyActive)
                {
                    // Disabling mode - restore baseline
                    await ShowStatusBarMessageAsync($"Disabling {mode} mode...");
                    _activeMode = null;
                    await RestoreBaselineAsync();
                    await ShowStatusBarMessageAsync($"Disabled {mode} mode - restored baseline settings");
                }
                else
                {
                    // Enabling mode
                    await ShowStatusBarMessageAsync($"Enabling {mode} mode...");

                    if (_activeMode.HasValue)
                    {
                        // Switching from one mode to another - restore baseline first
                        await RestoreBaselineAsync();
                    }

                    // Export current (now clean) settings as baseline before applying new mode
                    // Only export the settings that will be modified by this mode
                    await ExportBaselineAsync(mode);

                    _activeMode = mode;
                    await ApplyActiveModeAsync();
                    await ShowStatusBarMessageAsync($"Enabled {mode} mode");

                    // Execute mode-specific commands
                    await ExecuteModeCommandsAsync(mode, true);
                }

                // Persist and update UI
                await PersistActiveModeAsync();
                await UpdateStatusBarIndicatorAsync();
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                await VS.MessageBox.ShowErrorAsync("Modes", $"Error toggling {mode} mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears active mode without restoring baseline settings. Used when restoring from a backup file.
        /// </summary>
        public async Task ClearActiveModeWithoutRestoreAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                _activeMode = null;
                await PersistActiveModeAsync();
                await UpdateStatusBarIndicatorAsync();
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Exports current settings as the baseline backup, filtered to only include the settings that the specified
        /// mode will change.
        /// </summary>
        private async Task ExportBaselineAsync(ModeType mode)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Ensure the directory exists
                var dir = Path.GetDirectoryName(_baselineBackupPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Load the mode's settings file to use as a template for filtering
                var modeSettingsPath = _modeSettingsFiles[mode];
                if (!File.Exists(modeSettingsPath))
                {
                    // Fallback to full export
                    await VS.Commands.ExecuteAsync("Tools.ImportandExportSettings", $"/export:\"{_baselineBackupPath}\"");
                    return;
                }

                var modeSettingsDoc = new XmlDocument();
                modeSettingsDoc.Load(modeSettingsPath);

                // Export full settings to a temp file, then filter it
                var tempExportPath = Path.Combine(dir, "temp_full_export.vssettings");
                await VS.Commands.ExecuteAsync("Tools.ImportandExportSettings", $"/export:\"{tempExportPath}\"");

                // Wait for file to be written with retry logic instead of fixed delay
                var fileReady = await WaitForFileAsync(tempExportPath, timeoutMs: 5000);
                if (!fileReady)
                {
                    // Fallback to full export if temp file wasn't created in time
                    return;
                }

                // Filter the exported settings to match the mode's settings structure
                SettingsFilter.FilterSettingsFile(tempExportPath, _baselineBackupPath, modeSettingsDoc);
                File.Delete(tempExportPath);
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Waits for a file to exist and be accessible, with timeout.
        /// </summary>
        private static async Task<bool> WaitForFileAsync(string filePath, int timeoutMs)
        {
            const int checkIntervalMs = 50;
            var elapsed = 0;

            while (elapsed < timeoutMs)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        // Try to open the file to ensure it's not locked
                        using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            return true;
                        }
                    }
                }
                catch (IOException)
                {
                    // File exists but is locked, continue waiting
                }

                await Task.Delay(checkIntervalMs);
                elapsed += checkIntervalMs;
            }

            return File.Exists(filePath);
        }

        /// <summary>
        /// Restores the baseline settings.
        /// </summary>
        private async Task RestoreBaselineAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (File.Exists(_baselineBackupPath))
                {
                    await ImportSettingsAsync(_baselineBackupPath);
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Applies the active mode by importing its settings file.
        /// </summary>
        private async Task ApplyActiveModeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_activeMode.HasValue && _modeSettingsFiles.TryGetValue(_activeMode.Value, out var settingsPath))
            {
                if (File.Exists(settingsPath))
                {
                    await ImportSettingsAsync(settingsPath);
                }
            }
        }

        /// <summary>
        /// Imports a .vssettings file using DTE.
        /// </summary>
        private async Task ImportSettingsAsync(string settingsPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                await VS.Commands.ExecuteAsync("Tools.ImportandExportSettings", $"/import:\"{settingsPath}\"");
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Executes mode-specific commands (e.g., Focus mode hides tool windows).
        /// </summary>
        private async Task ExecuteModeCommandsAsync(ModeType mode, bool enabling)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                switch (mode)
                {
                    case ModeType.Focus:
                        if (enabling)
                        {
                            VS.Commands.ExecuteAsync("Window.AutoHideAll").FireAndForget();
                        }
                        break;

                    case ModeType.LowPower:
                        if (enabling)
                        {
                            VS.Commands.ExecuteAsync("Test.LiveUnitTesting.Stop").FireAndForget();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Persists the active mode to user settings.
        /// </summary>
        private async Task PersistActiveModeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                General options = await General.GetLiveInstanceAsync();
                options.ActiveModeName = _activeMode?.ToString();
                await options.SaveAsync();
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Shows an ephemeral message in the status bar.
        /// </summary>
        private async Task ShowStatusBarMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                IVsStatusbar statusbar = await VS.Services.GetStatusBarAsync();
                if (statusbar != null)
                {
                    statusbar.SetText(message);

                    // Clear after a few seconds
                    _ = Task.Delay(3000).ContinueWith(async _ =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        statusbar.SetText(string.Empty);
                    }, TaskScheduler.Default);
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Updates status bar indicator based on active mode.
        /// </summary>
        private async Task UpdateStatusBarIndicatorAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Remove existing indicator if present
                if (_statusBarIndicator != null)
                {
                    await StatusBarInjector.RemoveControlAsync(_statusBarIndicator);
                    _statusBarIndicator = null;
                }

                // Add indicator if a mode is active
                if (_activeMode.HasValue)
                {
                    _statusBarIndicator = CreateModeIndicator(_activeMode.Value);
                    await StatusBarInjector.InjectControlAsync(_statusBarIndicator);
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Creates a visual indicator for the status bar.
        /// </summary>
        private FrameworkElement CreateModeIndicator(ModeType mode)
        {
            string modeName;
            ImageMoniker moniker;

            switch (mode)
            {
                case ModeType.LowPower:
                    modeName = "Low Power";
                    moniker = KnownMonikers.PowerSupply;
                    break;
                case ModeType.Focus:
                    modeName = "Focus";
                    moniker = KnownMonikers.User;
                    break;
                case ModeType.Performance:
                    modeName = "Performance";
                    moniker = KnownMonikers.PerformanceWizard;
                    break;
                case ModeType.Presenter:
                    modeName = "Presenter";
                    moniker = KnownMonikers.InkPresenter;
                    break;
                default:
                    modeName = mode.ToString();
                    moniker = KnownMonikers.FlagOutline;
                    break;
            }

            var indicator = new CrispImage
            {
                Moniker = moniker,
                Width = 16,
                Height = 16,
                ToolTip = $"{modeName} Mode is active. Click to disable.",
                Margin = new Thickness(4, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Make it clickable to toggle the mode
            indicator.MouseLeftButtonUp += (s, e) =>
            {
                ToggleModeAsync(mode).FireAndForget();
            };

            return indicator;
        }
    }
}
