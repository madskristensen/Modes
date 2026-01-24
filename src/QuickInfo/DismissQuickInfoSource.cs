using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Modes.QuickInfo
{
    /// <summary>
    /// Dismisses Quick Info tooltips when Presenter mode is active.
    /// This prevents hover tooltips from obscuring code during presentations.
    /// </summary>
    internal sealed class DismissQuickInfoSource : IAsyncQuickInfoSource
    {
        public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            // Check if Presenter mode is active
            if (ModeManager.Instance.IsModeActive(ModeType.Presenter))
            {
                await session.DismissAsync();
            }

            return null;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
