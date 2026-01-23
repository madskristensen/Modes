using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Community.VisualStudio.Toolkit;
using EnvDTE80;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
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
        private const string CollectionPath = "Modes";
        private const string ActiveModesKey = "ActiveModes";

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
            _baselineBackupPath = Path.Combine(appDataPath, "Modes", "baseline.vssettings");
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

                if (!_settingsStore.CollectionExists(CollectionPath))
                {
                    _settingsStore.CreateCollection(CollectionPath);
                }

                // Load persisted active modes
                if (_settingsStore.PropertyExists(CollectionPath, ActiveModesKey))
                {
                    string savedModes = _settingsStore.GetString(CollectionPath, ActiveModesKey);

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
                    _activeModes.Clear();
                    await RestoreBaselineAsync();
                    await ShowStatusBarMessageAsync($"Disabled {mode} mode - restored baseline settings");
                }
                else
                {
                    // Enabling mode (mutually exclusive)
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
                    _settingsStore.SetString(CollectionPath, ActiveModesKey, modesString);
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
                List<ModeType> modesToRemove = new List<ModeType>();
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
            ImageMoniker moniker;
            string tooltip;

            switch (mode)
            {
                case ModeType.LowPower:
                    moniker = KnownMonikers.Battery;
                    tooltip = "Low Power Mode";
                    break;
                case ModeType.Focus:
                    moniker = KnownMonikers.ZoomIn;
                    tooltip = "Focus Mode";
                    break;
                case ModeType.Performance:
                    moniker = KnownMonikers.Performance;
                    tooltip = "Performance Mode";
                    break;
                case ModeType.Presenter:
                    moniker = KnownMonikers.FitToScreen;
                    tooltip = "Presenter Mode";
                    break;
                default:
                    moniker = KnownMonikers.Settings;
                    tooltip = mode.ToString();
                    break;
            }

            CrispImage indicator = new CrispImage
            {
                Moniker = moniker,
                Width = 14,
                Height = 14,
                ToolTip = tooltip,
                Margin = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Make it clickable to toggle the mode
            indicator.MouseLeftButtonUp += async (s, e) =>
            {
                await ToggleModeAsync(mode);
            };

            return indicator;
        }
    }
}
