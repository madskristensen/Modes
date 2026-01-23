using System;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Modes.Commands
{
    /// <summary>
    /// Base class for mode toggle commands that provides checkable menu item functionality.
    /// </summary>
    internal abstract class BaseModeCommand<T> : BaseCommand<T> where T : class, new()
    {
        /// <summary>
        /// Gets the mode type this command toggles.
        /// </summary>
        protected abstract ModeType Mode { get; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            // Update checked state based on whether mode is active
            Command.Checked = ModeManager.Instance.IsModeActive(Mode);
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ModeManager.Instance.ToggleModeAsync(Mode);
        }
    }
}
