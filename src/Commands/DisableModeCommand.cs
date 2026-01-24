using System;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Modes.Commands
{
    /// <summary>
    /// Command that disables the currently active mode.
    /// </summary>
    [Command(PackageIds.DisableModeCommand)]
    internal sealed class DisableModeCommand : BaseCommand<DisableModeCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            // Only enable the command when a mode is active
            Command.Enabled = ModeManager.Instance.ActiveMode.HasValue;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            ModeManager manager = ModeManager.Instance;

            if (manager.ActiveMode.HasValue)
            {
                await manager.ToggleModeAsync(manager.ActiveMode.Value);
            }
        }
    }
}
