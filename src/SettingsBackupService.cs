using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Community.VisualStudio.Toolkit;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Modes
{
    /// <summary>
    /// Service that automatically backs up user settings when the computer is idle.
    /// </summary>
    internal sealed class SettingsBackupService : IDisposable
    {
        private static SettingsBackupService _instance;
        private static readonly object _lock = new object();

        private readonly string _backupFolder;
        private readonly Timer _idleCheckTimer;
        private DateTime _lastBackupTime = DateTime.MinValue;
        private bool _disposed;

        public static SettingsBackupService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SettingsBackupService();
                        }
                    }
                }
                return _instance;
            }
        }

        private SettingsBackupService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _backupFolder = Path.Combine(appDataPath, Constants.Storage.AppDataFolderName, Constants.Backup.FolderName);

            // Ensure backup folder exists
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
            }

            // Load last backup time from most recent backup file
            LoadLastBackupTime();

            // Start idle check timer
            _idleCheckTimer = new Timer(OnIdleCheckTimer, null, Constants.Timers.IdleCheckIntervalMs, Constants.Timers.IdleCheckIntervalMs);
        }

        /// <summary>
        /// Gets the backup folder path.
        /// </summary>
        public string BackupFolder => _backupFolder;

        /// <summary>
        /// Gets all backup files sorted by date descending.
        /// </summary>
        public FileInfo[] GetBackupFiles()
        {
            if (!Directory.Exists(_backupFolder))
            {
                return Array.Empty<FileInfo>();
            }

            var dir = new DirectoryInfo(_backupFolder);
            FileInfo[] files = dir.GetFiles($"{Constants.Backup.FilePrefix}*{Constants.Backup.FileExtension}");
            Array.Sort(files, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
            return files;
        }

        /// <summary>
        /// Restores settings from a backup file.
        /// </summary>
        public async Task RestoreBackupAsync(string backupFilePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (!File.Exists(backupFilePath))
                {
                    await VS.MessageBox.ShowErrorAsync("Modes", "Backup file not found.");
                    return;
                }

                DTE2 dte = await VS.GetServiceAsync<EnvDTE.DTE, DTE2>();
                if (dte != null)
                {
                    dte.ExecuteCommand("Tools.ImportandExportSettings", $"/import:\"{backupFilePath}\"");
                    await VS.StatusBar.ShowMessageAsync($"Restored settings from {Path.GetFileName(backupFilePath)}");
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                await VS.MessageBox.ShowErrorAsync("Modes", $"Failed to restore settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Forces a backup now, regardless of idle state or time.
        /// </summary>
        public async Task ForceBackupAsync()
        {
            await CreateBackupAsync();
        }

        /// <summary>
        /// Creates a backup synchronously. For use from UI dialogs.
        /// </summary>
        public void CreateBackup()
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await CreateBackupAsync();
            });
        }

        private void LoadLastBackupTime()
        {
            FileInfo[] backups = GetBackupFiles();
            if (backups.Length > 0)
            {
                _lastBackupTime = backups[0].LastWriteTime;
            }
        }

        private void OnIdleCheckTimer(object state)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    General options = await General.GetLiveInstanceAsync();
                    if (!options.AutoBackupSettings)
                    {
                        return;
                    }

                    // Check if enough time has passed since last backup (48 hours default)
                    TimeSpan timeSinceLastBackup = DateTime.Now - _lastBackupTime;
                    if (timeSinceLastBackup.TotalHours < options.BackupIntervalHours)
                    {
                        return;
                    }

                    // Check if computer is idle
                    if (!IsComputerIdle())
                    {
                        return;
                    }

                    // Perform backup
                    await CreateBackupAsync();
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();
                }
            }).FireAndForget();
        }

        private async Task CreateBackupAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string backupFileName = $"{Constants.Backup.FilePrefix}{timestamp}{Constants.Backup.FileExtension}";
                string backupFilePath = Path.Combine(_backupFolder, backupFileName);

                DTE2 dte = await VS.GetServiceAsync<EnvDTE.DTE, DTE2>();
                if (dte != null)
                {
                    dte.ExecuteCommand("Tools.ImportandExportSettings", $"/export:\"{backupFilePath}\"");
                    _lastBackupTime = DateTime.Now;

                    // Clean up old backups
                    CleanupOldBackups(Constants.Backup.MaxBackupCount);
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private void CleanupOldBackups(int keepCount)
        {
            try
            {
                FileInfo[] backups = GetBackupFiles();
                for (int i = keepCount; i < backups.Length; i++)
                {
                    backups[i].Delete();
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private static bool IsComputerIdle()
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

            if (!GetLastInputInfo(ref lastInputInfo))
            {
                return false;
            }

            uint idleTime = (uint)Environment.TickCount - lastInputInfo.dwTime;
            return idleTime >= Constants.Timers.RequiredIdleTimeMs;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _idleCheckTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}
