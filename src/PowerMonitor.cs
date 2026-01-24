using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Windows.System.Power;
using Task = System.Threading.Tasks.Task;

namespace Modes
{
    /// <summary>
    /// Monitors Windows power state and automatically toggles Low Power mode
    /// based on Energy Saver status and power supply changes.
    /// </summary>
    internal sealed class PowerMonitor : IDisposable
    {
        private static PowerMonitor _instance;
        private static readonly object _lock = new object();

        private readonly AsyncPackage _package;
        private bool _wasLowPowerEnabledByPowerMonitor;
        private bool _disposed;

        public static PowerMonitor Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("PowerMonitor has not been initialized. Call Initialize first.");
                }
                return _instance;
            }
        }

        private PowerMonitor(AsyncPackage package)
        {
            _package = package;
        }

        /// <summary>
        /// Initializes the PowerMonitor singleton and subscribes to power events.
        /// </summary>
        public static Task<PowerMonitor> InitializeAsync(AsyncPackage package)
        {
            if (_instance != null)
            {
                return Task.FromResult(_instance);
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new PowerMonitor(package);
                }
            }

            // Subscribe to power events (only react to changes, not current state)
            PowerManager.EnergySaverStatusChanged += _instance.OnPowerStatusChanged;

            return Task.FromResult(_instance);
        }

        private void OnPowerStatusChanged(object sender, object e)
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await CheckPowerStateAsync();
            }).FireAndForget();
        }

        private async Task CheckPowerStateAsync()
        {
            try
            {
                General options = await General.GetLiveInstanceAsync();
                ModeManager manager = ModeManager.Instance;

                // Determine if we should be in low power mode based on settings
                var shouldEnableLowPower = ShouldEnableLowPowerMode(options);

                if (shouldEnableLowPower && !manager.IsModeActive(ModeType.LowPower))
                {
                    // Power state requires Low Power mode - enable it
                    _wasLowPowerEnabledByPowerMonitor = true;
                    await manager.ToggleModeAsync(ModeType.LowPower);
                }
                else if (!shouldEnableLowPower && _wasLowPowerEnabledByPowerMonitor && manager.IsModeActive(ModeType.LowPower))
                {
                    // Power state no longer requires Low Power mode - disable it if we enabled it
                    _wasLowPowerEnabledByPowerMonitor = false;
                    await manager.ToggleModeAsync(ModeType.LowPower);
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private static bool ShouldEnableLowPowerMode(General options)
        {
            // Check if Energy Saver is on and the setting is enabled
            if (options.AutoEnableLowPowerMode && PowerManager.EnergySaverStatus == EnergySaverStatus.On)
            {
                return true;
            }

            // Check if on battery and the setting is enabled
            if (options.AutoSwitchOnPowerSourceChange && PowerManager.PowerSupplyStatus == PowerSupplyStatus.NotPresent)
            {
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            PowerManager.EnergySaverStatusChanged -= OnPowerStatusChanged;

            // Clear singleton to allow re-initialization if package is reloaded
            lock (_lock)
            {
                if (_instance == this)
                {
                    _instance = null;
                }
            }
        }
    }
}
