using System;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Modes.Commands
{
    /// <summary>
    /// Command that resets all Visual Studio settings to their factory defaults.
    /// </summary>
    [Command(PackageIds.ResetToDefaultsCommand)]
    internal sealed class ResetToDefaultsCommand : BaseCommand<ResetToDefaultsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            VSConstants.MessageBoxResult result = await VS.MessageBox.ShowAsync(
                "Reset Visual Studio settings",
                "This will reset all Visual Studio settings to their factory defaults. Any active mode will be cleared and your customizations will be lost.\n\nDo you want to continue?",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

            if (result != VSConstants.MessageBoxResult.IDOK)
            {
                return;
            }

            try
            {
                ModeManager manager = ModeManager.Instance;
                if (manager.ActiveMode.HasValue)
                {
                    // Don't bother restoring the baseline; we're about to wipe everything.
                    await manager.ClearActiveModeWithoutRestoreAsync();
                }

                await VS.Commands.ExecuteAsync("Tools.ImportandExportSettings", "/reset");
                await VS.StatusBar.ShowMessageAsync("Visual Studio settings have been reset to defaults.");
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                await VS.MessageBox.ShowErrorAsync("Modes", $"Failed to reset settings: {ex.Message}");
            }
        }
    }
}
