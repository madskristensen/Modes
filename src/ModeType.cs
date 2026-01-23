namespace Modes
{
    /// <summary>
    /// Defines the available mode types.
    /// </summary>
    public enum ModeType
    {
        /// <summary>
        /// Optimizes for battery saving by disabling background tasks.
        /// </summary>
        LowPower,

        /// <summary>
        /// Minimizes distractions for focused coding.
        /// </summary>
        Focus,

        /// <summary>
        /// Maximizes build speed and responsiveness.
        /// </summary>
        Performance,

        /// <summary>
        /// Increases font sizes for presentations and screen sharing.
        /// </summary>
        Presenter
    }
}
