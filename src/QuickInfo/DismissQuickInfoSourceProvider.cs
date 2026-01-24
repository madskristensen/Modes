using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Modes.QuickInfo
{
    /// <summary>
    /// Provides the Quick Info source that dismisses tooltips in Presenter mode.
    /// </summary>
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name(nameof(DismissQuickInfoSourceProvider))]
    [ContentType("code")]
    [Order]
    internal sealed class DismissQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new DismissQuickInfoSource());
        }
    }
}
