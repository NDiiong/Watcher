using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Watcher
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class TextViewCreationListener : IWpfTextViewCreationListener
    {
        public const string LayerName = "ErrorHighlighter";
        [Import]
        public SVsServiceProvider serviceProvider { get; set; }

        [Import]
        public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(LayerName)]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out var document))
            {
                var svsErrorList = serviceProvider.GetService(typeof(SVsErrorList));
                if (svsErrorList is IErrorList errorList)
                {
                    var highlighter = new Highlighter(textView, document, errorList, serviceProvider);
                    document.FileActionOccurred += (s, e) =>
                    {
                        if (e.FileActionType == FileActionTypes.ContentSavedToDisk)
                            highlighter.Update(true);
                    };

                    textView.Closed += (s, e) => TextView_Closed(highlighter);
                }
            }
        }

        private void TextView_Closed(Highlighter highlighter)
        {
            if (highlighter != null)
                highlighter.Dispose();
        }
    }
}