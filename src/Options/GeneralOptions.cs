using System.ComponentModel;
using System.Runtime.InteropServices;
using Community.VisualStudio.Toolkit;

namespace Modes
{
    internal partial class OptionsProvider
    {
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    public class General : BaseOptionModel<General>
    {
        [Category("Power Management")]
        [DisplayName("Auto-enable Low Power mode")]
        [Description("Automatically enable Low Power mode when Windows enters power saver/battery saver mode.")]
        [DefaultValue(true)]
        public bool AutoEnableLowPowerMode { get; set; } = true;

        [Category("Power Management")]
        [DisplayName("Auto-switch on power source change")]
        [Description("Automatically enable Low Power mode when unplugging from AC power, and disable it when plugging back in.")]
        [DefaultValue(false)]
        public bool AutoSwitchOnPowerSourceChange { get; set; } = false;

        [Category("Backup")]
        [DisplayName("Auto-backup settings")]
        [Description("Automatically back up Visual Studio settings when the computer is idle.")]
        [DefaultValue(true)]
        public bool AutoBackupSettings { get; set; } = true;

        [Category("Backup")]
        [DisplayName("Backup interval (hours)")]
        [Description("Minimum time between automatic backups in hours.")]
        [DefaultValue(48)]
        public int BackupIntervalHours { get; set; } = 48;

        /// <summary>
        /// The currently active mode, if any. Stored as a string for persistence.
        /// </summary>
        [Browsable(false)]
        [DefaultValue(null)]
        public string ActiveModeName { get; set; }
    }
}
