namespace Modes
{
    /// <summary>
    /// Configuration constants for the Modes extension.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Settings backup configuration.
        /// </summary>
        public static class Backup
        {
            /// <summary>Folder name for storing backups under AppData.</summary>
            public const string FolderName = "Backups";

            /// <summary>Prefix for backup file names.</summary>
            public const string FilePrefix = "settings_";

            /// <summary>File extension for backup files.</summary>
            public const string FileExtension = ".vssettings";

            /// <summary>Maximum number of backup files to retain.</summary>
            public const int MaxBackupCount = 10;
        }

        /// <summary>
        /// Timer and idle detection intervals.
        /// </summary>
        public static class Timers
        {
            /// <summary>How often to check if the computer is idle (1 minute).</summary>
            public const int IdleCheckIntervalMs = 60_000;

            /// <summary>Required idle time before triggering a backup (5 minutes).</summary>
            public const int RequiredIdleTimeMs = 300_000;
        }

        /// <summary>
        /// Paths and storage keys.
        /// </summary>
        public static class Storage
        {
            /// <summary>Root folder name under AppData for Modes data.</summary>
            public const string AppDataFolderName = "Modes";

            /// <summary>Settings store collection path for persisted mode state.</summary>
            public const string SettingsCollectionPath = "Modes";

            /// <summary>Key for storing active modes.</summary>
            public const string ActiveModesKey = "ActiveModes";

            /// <summary>Baseline settings backup filename.</summary>
            public const string BaselineFileName = "baseline.vssettings";
        }
    }
}
