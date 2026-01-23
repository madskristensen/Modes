using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.PlatformUI;
using Task = System.Threading.Tasks.Task;

namespace Modes
{
    /// <summary>
    /// Injects custom WPF elements into the Visual Studio status bar.
    /// Adapted from https://github.com/madskristensen/DeveloperNews
    /// </summary>
    internal static class StatusBarInjector
    {
        private static Panel _panel;
        private static bool _isInitialized;

        /// <summary>
        /// Injects a FrameworkElement into the status bar (docked to the right).
        /// </summary>
        public static async Task<bool> InjectControlAsync(FrameworkElement element)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!_isInitialized)
            {
                _isInitialized = await EnsureUIAsync();
            }

            if (_panel == null)
            {
                return false;
            }

            // Check if the element is already in the panel
            if (!_panel.Children.Contains(element))
            {
                // Dock to the right side of the status bar
                element.SetValue(DockPanel.DockProperty, Dock.Right);

                // Insert at position 1 to appear after the resize grip (which is at position 0)
                _panel.Children.Insert(1, element);
            }

            return true;
        }

        /// <summary>
        /// Removes a FrameworkElement from the status bar.
        /// </summary>
        public static async Task<bool> RemoveControlAsync(FrameworkElement element)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_panel != null && _panel.Children.Contains(element))
            {
                _panel.Children.Remove(element);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ensures the status bar panel is available, with retry logic.
        /// </summary>
        private static async Task<bool> EnsureUIAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Try to find the status bar panel multiple times with a delay
            for (int i = 0; i < 10; i++)
            {
                _panel = FindChild<DockPanel>(Application.Current.MainWindow, "StatusBarPanel");

                if (_panel != null)
                {
                    return true;
                }

                await Task.Delay(500);
            }

            return false;
        }

        /// <summary>
        /// Recursively searches for a child element of a specific type and name.
        /// </summary>
        private static T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            T foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                var typedChild = child as T;
                if (typedChild == null)
                {
                    foundChild = FindChild<T>(child, childName);

                    if (foundChild != null)
                    {
                        break;
                    }
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                    {
                        foundChild = typedChild;
                        break;
                    }
                    else
                    {
                        foundChild = FindChild<T>(child, childName);

                        if (foundChild != null)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    foundChild = typedChild;
                    break;
                }
            }

            return foundChild;
        }
    }
}
