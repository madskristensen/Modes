using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Modes.Commands
{
    /// <summary>
    /// Command to show the backup selection dialog and restore settings.
    /// </summary>
    [Command(PackageIds.ResetSettingsCommand)]
    internal sealed class ResetSettingsCommand : BaseCommand<ResetSettingsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Show the backup selection dialog
            var dialog = new BackupSelectionDialog();

            // ShowModal properly parents to VS main window
            bool? result = dialog.ShowModal();

            if (result == true && !string.IsNullOrEmpty(dialog.SelectedBackupPath))
            {
                // Disable any active mode first
                ModeManager manager = ModeManager.Instance;
                foreach (ModeType mode in new[] { ModeType.LowPower, ModeType.Focus, ModeType.Performance, ModeType.Presenter })
                {
                    if (manager.IsModeActive(mode))
                    {
                        // Clear active mode without restoring baseline (we're about to restore a different backup)
                        await manager.ClearActiveModeWithoutRestoreAsync();
                        break;
                    }
                }

                // Restore the selected backup
                await SettingsBackupService.Instance.RestoreBackupAsync(dialog.SelectedBackupPath);
            }
        }
    }
}
