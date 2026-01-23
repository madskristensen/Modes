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
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Modes", "General", 0, 0, true, SupportsProfiles = true)]

    // Auto-load when shell is initialized (background load keeps it lightweight)
    [ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class ModesPackage : ToolkitPackage
    {
        private bool _wasLowPowerEnabledBySystem;
        private SettingsBackupService _backupService;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await VS.MessageBox.ShowAsync("Modes Package Initializing...");
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
                    bool isWindowsInPowerSaver = IsWindowsInPowerSaverMode();

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
