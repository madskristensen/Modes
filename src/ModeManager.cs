using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Community.VisualStudio.Toolkit;
using EnvDTE80;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
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

        private readonly HashSet<ModeType> _activeModes = new HashSet<ModeType>();
        private readonly Dictionary<ModeType, FrameworkElement> _statusBarIndicators = new Dictionary<ModeType, FrameworkElement>();
        private readonly Dictionary<ModeType, string> _modeSettingsFiles;
        private readonly string _baselineBackupPath;

        private WritableSettingsStore _settingsStore;

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
            string extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string settingsDir = Path.Combine(extensionDir, "Settings");

            _modeSettingsFiles = new Dictionary<ModeType, string>
            {
                { ModeType.LowPower, Path.Combine(settingsDir, "LowPower.vssettings") },
                { ModeType.Focus, Path.Combine(settingsDir, "Focus.vssettings") },
                { ModeType.Performance, Path.Combine(settingsDir, "Performance.vssettings") },
                { ModeType.Presenter, Path.Combine(settingsDir, "Presenter.vssettings") }
            };

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baselineBackupPath = Path.Combine(appDataPath, Constants.Storage.AppDataFolderName, Constants.Storage.BaselineFileName);
        }

        /// <summary>
        /// Gets whether a specific mode is active.
        /// </summary>
        public bool IsModeActive(ModeType mode) => _activeModes.Contains(mode);

        /// <summary>
        /// Gets the currently active modes.
        /// </summary>
        public IReadOnlyCollection<ModeType> ActiveModes => _activeModes;

        /// <summary>
        /// Initializes the manager and loads persisted mode states.
        /// </summary>
        public async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                SettingsManager settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
                _settingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

                if (!_settingsStore.CollectionExists(Constants.Storage.SettingsCollectionPath))
                {
                    _settingsStore.CreateCollection(Constants.Storage.SettingsCollectionPath);
                }

                // Load persisted active modes
                if (_settingsStore.PropertyExists(Constants.Storage.SettingsCollectionPath, Constants.Storage.ActiveModesKey))
                {
                    string savedModes = _settingsStore.GetString(Constants.Storage.SettingsCollectionPath, Constants.Storage.ActiveModesKey);

                    if (!string.IsNullOrEmpty(savedModes))
                    {
                        foreach (string modeStr in savedModes.Split(','))
                        {
                            if (Enum.TryParse(modeStr.Trim(), out ModeType mode))
                            {
                                _activeModes.Add(mode);
                            }
                        }
                    }
                }

                // Apply active modes and update UI
                if (_activeModes.Count > 0)
                {
                    await ApplyActiveModesAsync();
                    await UpdateStatusBarIndicatorsAsync();
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
                bool wasEmpty = _activeModes.Count == 0;
                bool isCurrentlyActive = _activeModes.Contains(mode);

                if (isCurrentlyActive)
                {
                    // Disabling mode - restore baseline
                    await ShowStatusBarMessageAsync($"Disabling {mode} mode...");
                    _activeModes.Clear();
                    await RestoreBaselineAsync();
                    await ShowStatusBarMessageAsync($"Disabled {mode} mode - restored baseline settings");
                }
                else
                {
                    // Enabling mode (mutually exclusive)
                    await ShowStatusBarMessageAsync($"Enabling {mode} mode...");

                    if (wasEmpty)
                    {
                        // Export current settings as baseline before applying first mode
                        await ExportBaselineAsync();
                    }

                    // Clear any other active mode and set this one
                    _activeModes.Clear();
                    _activeModes.Add(mode);
                    await ApplyActiveModesAsync();
                    await ShowStatusBarMessageAsync($"Enabled {mode} mode");

                    // Execute mode-specific commands
                    await ExecuteModeCommandsAsync(mode, true);
                }

                // Persist and update UI
                await PersistActiveModesAsync();
                await UpdateStatusBarIndicatorsAsync();
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                await VS.MessageBox.ShowErrorAsync("Modes", $"Error toggling {mode} mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears active modes without restoring baseline settings.
        /// Used when restoring from a backup file.
        /// </summary>
        public async Task ClearActiveModeWithoutRestoreAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                _activeModes.Clear();
                await PersistActiveModesAsync();
                await UpdateStatusBarIndicatorsAsync();
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        /// <summary>
        /// Exports current settings as the baseline backup.
        /// </summary>
        private async Task ExportBaselineAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Ensure the directory exists
                string dir = Path.GetDirectoryName(_baselineBackupPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                DTE2 dte = await VS.GetServiceAsync<EnvDTE.DTE, DTE2>();
                if (dte != null)
                {
                    // Export current settings using DTE
                    dte.ExecuteCommand("Tools.ImportandExportSettings", $"/export:\"{_baselineBackupPath}\"");
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
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
        /// Applies all active modes by importing their settings files in order.
        /// </summary>
        private async Task ApplyActiveModesAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Apply modes in a specific order: LowPower -> Focus -> Performance -> Presenter
            ModeType[] orderedModes = { ModeType.LowPower, ModeType.Focus, ModeType.Performance, ModeType.Presenter };

            foreach (ModeType mode in orderedModes)
            {
                if (_activeModes.Contains(mode) && _modeSettingsFiles.TryGetValue(mode, out string settingsPath))
                {
                    if (File.Exists(settingsPath))
                    {
                        await ImportSettingsAsync(settingsPath);
                    }
                }
            }
        }

        /// <summary>
        /// Reapplies all modes from baseline (used when disabling a mode).
        /// </summary>
        private async Task ReapplyAllModesAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // First restore baseline
            if (File.Exists(_baselineBackupPath))
            {
                await ImportSettingsAsync(_baselineBackupPath);
            }

            // Then apply all active modes
            await ApplyActiveModesAsync();
        }

        /// <summary>
        /// Imports a .vssettings file using DTE.
        /// </summary>
        private async Task ImportSettingsAsync(string settingsPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                DTE2 dte = await VS.GetServiceAsync<EnvDTE.DTE, DTE2>();
                if (dte != null)
                {
                    dte.ExecuteCommand("Tools.ImportandExportSettings", $"/import:\"{settingsPath}\"");
                }
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
                DTE2 dte = await VS.GetServiceAsync<EnvDTE.DTE, DTE2>();
                if (dte == null) return;

                switch (mode)
                {
                    case ModeType.Focus:
                        if (enabling)
                        {
                            // Auto-hide all tool windows for distraction-free coding
                            try
                            {
                                dte.ExecuteCommand("Window.AutoHideAll");
                            }
                            catch
                            {
                                // Command may not be available in all VS configurations
                            }
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
        /// Persists the active modes to user settings.
        /// </summary>
        private async Task PersistActiveModesAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (_settingsStore != null)
                {
                    string modesString = string.Join(",", _activeModes);
                    _settingsStore.SetString(Constants.Storage.SettingsCollectionPath, Constants.Storage.ActiveModesKey, modesString);
                }
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
        /// Updates status bar indicators based on active modes.
        /// </summary>
        private async Task UpdateStatusBarIndicatorsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Remove indicators for inactive modes
                var modesToRemove = new List<ModeType>();
                foreach (KeyValuePair<ModeType, FrameworkElement> kvp in _statusBarIndicators)
                {
                    if (!_activeModes.Contains(kvp.Key))
                    {
                        await StatusBarInjector.RemoveControlAsync(kvp.Value);
                        modesToRemove.Add(kvp.Key);
                    }
                }

                foreach (ModeType mode in modesToRemove)
                {
                    _statusBarIndicators.Remove(mode);
                }

                // Add indicators for active modes that don't have them
                foreach (ModeType mode in _activeModes)
                {
                    if (!_statusBarIndicators.ContainsKey(mode))
                    {
                        FrameworkElement indicator = CreateModeIndicator(mode);
                        if (await StatusBarInjector.InjectControlAsync(indicator))
                        {
                            _statusBarIndicators[mode] = indicator;
                        }
                    }
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

            switch (mode)
            {
                case ModeType.LowPower:
                    modeName = "Low Power";
                    break;
                case ModeType.Focus:
                    modeName = "Focus";
                    break;
                case ModeType.Performance:
                    modeName = "Performance";
                    break;
                case ModeType.Presenter:
                    modeName = "Presenter";
                    break;
                default:
                    modeName = mode.ToString();
                    break;
            }

            var indicator = new CrispImage
            {
                Moniker = KnownMonikers.BooleanData,
                Width = 16,
                Height = 16,
                ToolTip = $"{modeName} Mode is active. Click to disable.",
                Margin = new Thickness(4, 0, 4, 0),
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
