using System;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Modes
{
    [Command(PackageIds.SettingsCommand)]
    internal sealed class SettingsCommand : BaseCommand<SettingsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Open the Modes > General options page
            Package.ShowOptionPage(typeof(OptionsProvider.GeneralOptions));
        }
    }
}
