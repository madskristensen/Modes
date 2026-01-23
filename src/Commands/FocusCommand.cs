using Community.VisualStudio.Toolkit;

namespace Modes.Commands
{
    /// <summary>
    /// Command to toggle Focus mode.
    /// </summary>
    [Command(PackageIds.FocusCommand)]
    internal sealed class FocusCommand : BaseModeCommand<FocusCommand>
    {
        protected override ModeType Mode => ModeType.Focus;
    }
}
