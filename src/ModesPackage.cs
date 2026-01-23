using System;
using System.Runtime.InteropServices;
using System.Threading;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace Modes
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.ModesString)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), Vsix.Name, "General", 0, 0, true)]
    [ProvideProfile(typeof(OptionsProvider.GeneralOptions), Vsix.Name, "General", 0, 0, true)]

    [ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]

    // Rule-based UI context: Load immediately when a mode is active (checks BaseOptionModel settings store)
    //[ProvideAutoLoad(PackageGuids.ModeActiveUIContextString, PackageAutoLoadFlags.BackgroundLoad)]
    //[ProvideUIContextRule(
    //    PackageGuids.ModeActiveUIContextString,
    //    name: "Mode Active",
    //    expression: "ModeActive",
    //    termNames: new[] { "ActiveModeNme" },
    //    termValues: new[] { @"UserSettingsStoreQuery:Modes.General\ModeActive" })]

    // Rule-based UI context: Load after delay for backup functionality when no mode is active
    //[ProvideAutoLoad(PackageGuids.BackupDelayedUIContextString, PackageAutoLoadFlags.BackgroundLoad)]
    //[ProvideUIContextRule(
    //    PackageGuids.BackupDelayedUIContextString,
    //    name: "Backup Delayed",
    //    expression: "ShellInitialized",
    //    termNames: new[] { "ShellInitialized" },
    //    termValues: new[] { Microsoft.VisualStudio.VSConstants.UICONTEXT.ShellInitialized_string },
    //    delay: 3600000)] // 1 hour delay
    public sealed class ModesPackage : ToolkitPackage
    {
        private bool _wasLowPowerEnabledBySystem;
        private SettingsBackupService _backupService;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();

            // Initialize the ModeManager to restore persisted mode states
            await ModeManager.Instance.InitializeAsync();

            // Initialize the backup service
            _backupService = SettingsBackupService.Instance;

            // Subscribe to Windows power mode changes
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                _backupService?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                General options = await General.GetLiveInstanceAsync();

                if (!options.AutoEnableLowPowerMode)
                {
                    return;
                }

                ModeManager manager = ModeManager.Instance;

                if (e.Mode == PowerModes.StatusChange)
                {
                    // Check if Windows is in battery saver / power saver mode
                    var isWindowsInPowerSaver = IsWindowsInPowerSaverMode();

                    if (isWindowsInPowerSaver && !manager.IsModeActive(ModeType.LowPower))
                    {
                        // Windows entered power saver mode - enable Low Power mode
                        _wasLowPowerEnabledBySystem = true;
                        await manager.ToggleModeAsync(ModeType.LowPower);
                    }
                    else if (!isWindowsInPowerSaver && _wasLowPowerEnabledBySystem && manager.IsModeActive(ModeType.LowPower))
                    {
                        // Windows left power saver mode - disable Low Power mode if we enabled it
                        _wasLowPowerEnabledBySystem = false;
                        await manager.ToggleModeAsync(ModeType.LowPower);
                    }
                }
            }).FireAndForget();
        }

        private static bool IsWindowsInPowerSaverMode()
        {
            try
            {
                // Check Windows power saver status via GetSystemPowerStatus
                if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
                {
                    // SystemStatusFlag: 1 = Battery Saver is on
                    return status.SystemStatusFlag == 1;
                }
            }
            catch
            {
                // Ignore errors
            }

            return false;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }
    }
}
