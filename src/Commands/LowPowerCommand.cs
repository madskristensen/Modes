using Community.VisualStudio.Toolkit;

namespace Modes.Commands
{
    /// <summary>
    /// Command to toggle Low Power mode.
    /// </summary>
    [Command(PackageIds.LowPowerCommand)]
    internal sealed class LowPowerCommand : BaseModeCommand<LowPowerCommand>
    {
        protected override ModeType Mode => ModeType.LowPower;
    }
}
