using Community.VisualStudio.Toolkit;

namespace Modes.Commands
{
    /// <summary>
    /// Command to toggle Presenter mode.
    /// </summary>
    [Command(PackageIds.PresenterCommand)]
    internal sealed class PresenterCommand : BaseModeCommand<PresenterCommand>
    {
        protected override ModeType Mode => ModeType.Presenter;
    }
}
