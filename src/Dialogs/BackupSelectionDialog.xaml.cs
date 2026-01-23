using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;

namespace Modes
{
    /// <summary>
    /// Interaction logic for BackupSelectionDialog.xaml
    /// </summary>
    public partial class BackupSelectionDialog : DialogWindow
    {
        private readonly List<BackupFileItem> _backupItems = new List<BackupFileItem>();

        public BackupSelectionDialog()
        {
            InitializeComponent();
            LoadBackups();
        }

        /// <summary>
        /// Gets the selected backup file path, or null if none selected.
        /// </summary>
        public string SelectedBackupPath { get; private set; }

        private void LoadBackups()
        {
            _backupItems.Clear();

            FileInfo[] backups = SettingsBackupService.Instance.GetBackupFiles();
            foreach (FileInfo backup in backups)
            {
                _backupItems.Add(new BackupFileItem
                {
                    FilePath = backup.FullName,
                    DisplayDate = backup.LastWriteTime.ToString("f", CultureInfo.CurrentCulture),
                    FileSize = FormatFileSize(backup.Length),
                    LastWriteTime = backup.LastWriteTime
                });
            }

            BackupListBox.ItemsSource = _backupItems;

            if (_backupItems.Count == 0)
            {
                // Show message if no backups available
                BackupListBox.Visibility = Visibility.Collapsed;
                RestoreButton.IsEnabled = false;
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }

        private void BackupListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selected = BackupListBox.SelectedItem as BackupFileItem;
            RestoreButton.IsEnabled = selected != null;

            if (selected != null)
            {
                SelectedBackupPathText.Text = selected.FilePath;
            }
        }

        private void BackupListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BackupListBox.SelectedItem != null)
            {
                RestoreButton_Click(sender, e);
            }
        }

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CreateBackupButton.IsEnabled = false;
                CreateBackupButton.Content = "Creating...";

                await SettingsBackupService.Instance.CreateBackupAndRefreshAsync();

                // Brief delay to ensure file system is updated
                await System.Threading.Tasks.Task.Delay(100);

                LoadBackups();

                // Select the newly created backup (first in the list)
                if (_backupItems.Count > 0)
                {
                    BackupListBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create backup: {ex.Message}", "Modes", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CreateBackupButton.IsEnabled = true;
                CreateBackupButton.Content = "Create Backup Now";
            }
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string folder = SettingsBackupService.Instance.BackupFolder;
            if (Directory.Exists(folder))
            {
                Process.Start("explorer.exe", folder);
            }
            else
            {
                MessageBox.Show("Backup folder does not exist yet.", "Modes", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (BackupListBox.SelectedItem is BackupFileItem selected)
            {
                SelectedBackupPath = selected.FilePath;
                DialogResult = true;
                Close();
            }
        }
    }

    /// <summary>
    /// Represents a backup file item for display in the list.
    /// </summary>
    public class BackupFileItem
    {
        public string FilePath { get; set; }
        public string DisplayDate { get; set; }
        public string FileSize { get; set; }
        public DateTime LastWriteTime { get; set; }
    }

    /// <summary>
    /// Converts null to Collapsed and non-null to Visible.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
