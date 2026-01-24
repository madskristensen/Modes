using System;
using System.Runtime.InteropServices;
using System.Threading;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
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
        private SettingsBackupService _backupService;
        private PowerMonitor _powerMonitor;
        private ModeManager _modeManager;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();

            // Initialize the ModeManager to restore persisted mode states
            _modeManager = ModeManager.Instance;
            await _modeManager.InitializeAsync();

            // Initialize the backup service
            _backupService = SettingsBackupService.Instance;

            // Initialize power monitoring (also checks current state on startup)
            _powerMonitor = await PowerMonitor.InitializeAsync(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _powerMonitor?.Dispose();
                _backupService?.Dispose();
                _modeManager?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
