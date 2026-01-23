using Community.VisualStudio.Toolkit;

namespace Modes.Commands
{
    /// <summary>
    /// Command to toggle Performance mode.
    /// </summary>
    [Command(PackageIds.PerformanceCommand)]
    internal sealed class PerformanceCommand : BaseModeCommand<PerformanceCommand>
    {
        protected override ModeType Mode => ModeType.Performance;
    }
}
