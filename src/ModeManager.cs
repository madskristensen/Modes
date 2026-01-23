using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Community.VisualStudio.Toolkit;
using EnvDTE80;
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
                bool isCurrentlyActive = _activeMode == mode;

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
        /// Clears active mode without restoring baseline settings.
        /// Used when restoring from a backup file.
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
        /// Exports current settings as the baseline backup, filtered to only include
        /// the settings that the specified mode will change.
        /// </summary>
        private async Task ExportBaselineAsync(ModeType mode)
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
                if (dte == null)
                {
                    return;
                }

                // Load the mode's settings file to use as a template for filtering
                string modeSettingsPath = _modeSettingsFiles[mode];
                if (!File.Exists(modeSettingsPath))
                {
                    // Fallback to full export
                    dte.ExecuteCommand("Tools.ImportandExportSettings", $"/export:\"{_baselineBackupPath}\"");
                    return;
                }

                var modeSettingsDoc = new XmlDocument();
                modeSettingsDoc.Load(modeSettingsPath);

                // Export full settings to a temp file, then filter it
                string tempExportPath = Path.Combine(dir, "temp_full_export.vssettings");
                dte.ExecuteCommand("Tools.ImportandExportSettings", $"/export:\"{tempExportPath}\"");

                // Wait for file to be written with retry logic instead of fixed delay
                bool fileReady = await WaitForFileAsync(tempExportPath, timeoutMs: 5000);
                if (!fileReady)
                {
                    // Fallback to full export if temp file wasn't created in time
                    return;
                }

                // Filter the exported settings to match the mode's settings structure
                FilterSettingsFile(tempExportPath, _baselineBackupPath, modeSettingsDoc);
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
            int elapsed = 0;

            while (elapsed < timeoutMs)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        // Try to open the file to ensure it's not locked
                        using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
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
        /// Filters a vssettings file to only keep the exact properties defined in the mode's settings.
        /// </summary>
        private void FilterSettingsFile(string sourcePath, string destPath, XmlDocument modeSettings)
        {
            try
            {
                var exportDoc = new XmlDocument();
                exportDoc.Load(sourcePath);

                XmlNode exportUserSettings = exportDoc.SelectSingleNode("/UserSettings");
                XmlNode modeUserSettings = modeSettings.SelectSingleNode("/UserSettings");

                if (exportUserSettings == null || modeUserSettings == null)
                {
                    File.Copy(sourcePath, destPath, true);
                    return;
                }

                // Build lookup of properties to keep from mode settings
                // Key: "ParentCategory/SubCategory/PropertyName", Value: true
                var propertiesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Extract property paths from mode's ToolsOptions
                XmlNode modeToolsOptions = modeUserSettings.SelectSingleNode("ToolsOptions");
                if (modeToolsOptions != null)
                {
                    foreach (XmlNode category in modeToolsOptions.SelectNodes("ToolsOptionsCategory"))
                    {
                        string categoryName = category.Attributes?["name"]?.Value;
                        if (string.IsNullOrEmpty(categoryName)) continue;

                        foreach (XmlNode subCategory in category.SelectNodes("ToolsOptionsSubCategory"))
                        {
                            string subCategoryName = subCategory.Attributes?["name"]?.Value;
                            if (string.IsNullOrEmpty(subCategoryName)) continue;

                            foreach (XmlNode prop in subCategory.SelectNodes("PropertyValue"))
                            {
                                string propName = prop.Attributes?["name"]?.Value;
                                if (!string.IsNullOrEmpty(propName))
                                {
                                    propertiesToKeep.Add($"{categoryName}/{subCategoryName}/{propName}");
                                }
                            }
                        }
                    }
                }

                // Extract FontsAndColors category GUIDs to keep (handle both GUID and Guid attributes)
                // The structure can be: UserSettings/Category[@name='Environment_Group']/Category[@name='Environment_FontsAndColors']
                // or directly: UserSettings/Category[@name='Environment_FontsAndColors']
                var fontCategoryGuidsToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                XmlNode modeFontsCategory = modeUserSettings.SelectSingleNode(".//Category[@name='Environment_FontsAndColors']");
                if (modeFontsCategory != null)
                {
                    foreach (XmlNode fontCategory in modeFontsCategory.SelectNodes(".//Category[@GUID or @Guid]"))
                    {
                        // Try both GUID (uppercase) and Guid (mixed case) attributes
                        string guid = fontCategory.Attributes?["GUID"]?.Value 
                                   ?? fontCategory.Attributes?["Guid"]?.Value;
                        if (!string.IsNullOrEmpty(guid))
                        {
                            fontCategoryGuidsToKeep.Add(guid);
                        }
                    }
                }

                // Extract top-level Category names to keep from mode settings
                var topLevelCategoriesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (XmlNode modeCategory in modeUserSettings.SelectNodes("Category"))
                {
                    string categoryName = modeCategory.Attributes?["name"]?.Value;
                    if (!string.IsNullOrEmpty(categoryName))
                    {
                        topLevelCategoriesToKeep.Add(categoryName);
                    }
                }

                // Filter ToolsOptions in the export to only keep matching properties
                XmlNode exportToolsOptions = exportUserSettings.SelectSingleNode("ToolsOptions");
                if (exportToolsOptions != null)
                {
                    if (propertiesToKeep.Count == 0)
                    {
                        // Mode has no ToolsOptions settings, clear all ToolsOptionsCategory children
                        exportToolsOptions.RemoveAll();
                    }
                    else
                    {
                        var categoriesToRemove = new List<XmlNode>();

                        foreach (XmlNode category in exportToolsOptions.SelectNodes("ToolsOptionsCategory"))
                        {
                            string categoryName = category.Attributes?["name"]?.Value;
                            if (string.IsNullOrEmpty(categoryName)) continue;

                            var subCategoriesToRemove = new List<XmlNode>();

                            foreach (XmlNode subCategory in category.SelectNodes("ToolsOptionsSubCategory"))
                            {
                                string subCategoryName = subCategory.Attributes?["name"]?.Value;
                                if (string.IsNullOrEmpty(subCategoryName)) continue;

                                var propsToRemove = new List<XmlNode>();

                                foreach (XmlNode prop in subCategory.SelectNodes("PropertyValue"))
                                {
                                    string propName = prop.Attributes?["name"]?.Value;
                                    string fullPath = $"{categoryName}/{subCategoryName}/{propName}";

                                    if (!propertiesToKeep.Contains(fullPath))
                                    {
                                        propsToRemove.Add(prop);
                                    }
                                }

                                foreach (XmlNode node in propsToRemove)
                                {
                                    subCategory.RemoveChild(node);
                                }

                                // Remove subcategory if no properties left
                                if (subCategory.SelectNodes("PropertyValue").Count == 0)
                                {
                                    subCategoriesToRemove.Add(subCategory);
                                }
                            }

                            foreach (XmlNode node in subCategoriesToRemove)
                            {
                                category.RemoveChild(node);
                            }

                            // Remove category if no subcategories left
                            if (category.SelectNodes("ToolsOptionsSubCategory").Count == 0)
                            {
                                categoriesToRemove.Add(category);
                            }
                        }

                        foreach (XmlNode node in categoriesToRemove)
                        {
                            exportToolsOptions.RemoveChild(node);
                        }
                    }
                }

                // Filter top-level Categories - keep ones defined in mode settings
                var topLevelToRemove = new List<XmlNode>();
                foreach (XmlNode category in exportUserSettings.SelectNodes("Category"))
                {
                    string categoryName = category.Attributes?["name"]?.Value;

                    if (categoryName == "Environment_Group" && fontCategoryGuidsToKeep.Count > 0)
                    {
                        // Find Environment_FontsAndColors inside Environment_Group
                        XmlNode fontsColorCategory = category.SelectSingleNode("Category[@name='Environment_FontsAndColors']");
                        if (fontsColorCategory != null)
                        {
                            // Filter the FontsAndColors to only keep matching GUIDs
                            XmlNode fontsAndColors = fontsColorCategory.SelectSingleNode("FontsAndColors");
                            if (fontsAndColors != null)
                            {
                                var fontCategoriesToRemove = new List<XmlNode>();
                                // Categories are inside Categories/Category elements
                                foreach (XmlNode fontCategory in fontsAndColors.SelectNodes(".//Category[@GUID or @Guid]"))
                                {
                                    string guid = fontCategory.Attributes?["GUID"]?.Value 
                                               ?? fontCategory.Attributes?["Guid"]?.Value;
                                    if (!string.IsNullOrEmpty(guid) && !fontCategoryGuidsToKeep.Contains(guid))
                                    {
                                        fontCategoriesToRemove.Add(fontCategory);
                                    }
                                }

                                foreach (XmlNode node in fontCategoriesToRemove)
                                {
                                    node.ParentNode.RemoveChild(node);
                                }
                            }
                        }

                        // Remove other categories inside Environment_Group (like Environment_Aliases, etc.)
                        var otherCategoriesToRemove = new List<XmlNode>();
                        foreach (XmlNode innerCategory in category.SelectNodes("Category"))
                        {
                            string innerName = innerCategory.Attributes?["name"]?.Value;
                            if (innerName != "Environment_FontsAndColors")
                            {
                                otherCategoriesToRemove.Add(innerCategory);
                            }
                        }
                        foreach (XmlNode node in otherCategoriesToRemove)
                        {
                            category.RemoveChild(node);
                        }
                    }
                    else if (topLevelCategoriesToKeep.Contains(categoryName))
                    {
                        // Keep this category - it's defined in the mode settings
                        // Filter properties to only keep ones defined in the mode
                        XmlNode modeCategory = modeUserSettings.SelectSingleNode($"Category[@name='{categoryName}']");
                        if (modeCategory != null)
                        {
                            var modePropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (XmlNode prop in modeCategory.SelectNodes("PropertyValue"))
                            {
                                string propName = prop.Attributes?["name"]?.Value;
                                if (!string.IsNullOrEmpty(propName))
                                {
                                    modePropertyNames.Add(propName);
                                }
                            }

                            // Remove properties not in mode settings
                            var propsToRemove = new List<XmlNode>();
                            foreach (XmlNode prop in category.SelectNodes("PropertyValue"))
                            {
                                string propName = prop.Attributes?["name"]?.Value;
                                if (!modePropertyNames.Contains(propName))
                                {
                                    propsToRemove.Add(prop);
                                }
                            }
                            foreach (XmlNode node in propsToRemove)
                            {
                                category.RemoveChild(node);
                            }
                        }
                    }
                    else
                    {
                        // Remove other top-level categories entirely
                        topLevelToRemove.Add(category);
                    }
                }

                foreach (XmlNode node in topLevelToRemove)
                {
                    exportUserSettings.RemoveChild(node);
                }

                exportDoc.Save(destPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to filter settings file: {ex.Message}");
                File.Copy(sourcePath, destPath, true);
            }
        }

        /// <summary>
        /// Parses a .vssettings file and extracts the category paths for filtering exports.
        /// Returns paths in the format "Category/SubCategory" as used by VS export command.
        /// </summary>
        private List<string> GetCategoriesFromSettingsFile(string settingsFilePath)
        {
            var categories = new List<string>();

            try
            {
                if (!File.Exists(settingsFilePath))
                {
                    return categories;
                }

                var doc = new XmlDocument();
                doc.Load(settingsFilePath);

                // Parse ToolsOptions categories (e.g., Environment/General, TextEditor/CSharp)
                XmlNodeList toolsOptionsCategoryNodes = doc.SelectNodes("/UserSettings/ToolsOptions/ToolsOptionsCategory");
                if (toolsOptionsCategoryNodes != null)
                {
                    foreach (XmlNode category in toolsOptionsCategoryNodes)
                    {
                        string categoryName = category.Attributes?["name"]?.Value;
                        if (string.IsNullOrEmpty(categoryName))
                        {
                            continue;
                        }

                        // Get all subcategories within this category
                        XmlNodeList subCategoryNodes = category.SelectNodes("ToolsOptionsSubCategory");
                        if (subCategoryNodes != null && subCategoryNodes.Count > 0)
                        {
                            foreach (XmlNode subCategory in subCategoryNodes)
                            {
                                string subCategoryName = subCategory.Attributes?["name"]?.Value;
                                if (!string.IsNullOrEmpty(subCategoryName))
                                {
                                    // Format: "Environment/General" or "TextEditor/CSharp"
                                    categories.Add($"{categoryName}/{subCategoryName}");
                                }
                            }
                        }
                        else
                        {
                            // Category without subcategories
                            categories.Add(categoryName);
                        }
                    }
                }

                // Parse top-level Category elements (e.g., Environment_FontsAndColors)
                // These are used for fonts, colors, keybindings, etc.
                XmlNodeList topLevelCategoryNodes = doc.SelectNodes("/UserSettings/Category");
                if (topLevelCategoryNodes != null)
                {
                    foreach (XmlNode category in topLevelCategoryNodes)
                    {
                        string categoryName = category.Attributes?["name"]?.Value;
                        if (!string.IsNullOrEmpty(categoryName))
                        {
                            categories.Add(categoryName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse settings file: {ex.Message}");
            }

            return categories;
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

            if (_activeMode.HasValue && _modeSettingsFiles.TryGetValue(_activeMode.Value, out string settingsPath))
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
