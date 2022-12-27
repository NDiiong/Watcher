using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using SVsServiceProvider = Microsoft.VisualStudio.Shell.SVsServiceProvider;

namespace Watcher
{
    internal class Highlighter : IDisposable
    {
        private bool _processing;
        private readonly Adornment _text;
        private readonly IWpfTextView _view;
        private readonly ITextDocument _document;
        private readonly IErrorList _errorList;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly Dispatcher _dispatcher;

        public Highlighter(IWpfTextView view, ITextDocument document, IErrorList errorList, SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _view = view;
            _document = document;
            _errorList = errorList;
            _serviceProvider = serviceProvider;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _text = new Adornment();
            _text.MouseUp += text_MouseUp;

            _adornmentLayer = view.GetAdornmentLayer(TextViewCreationListener.LayerName);
            if (_adornmentLayer.IsEmpty)
                _adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, _text, null);

            _view.ViewportHeightChanged += SetAdornmentLocation;
            _view.ViewportWidthChanged += SetAdornmentLocation;

            errorList.TableControl.EntriesChanged += TableControl_EntriesChanged;
        }

        public void Update(bool highlight)
        {
            if (!highlight && _processing)
                return;

            _processing = true;

            var entries = _errorList.TableControl.Entries.ToArray();
            UpdateAdornment(highlight, entries);

            _processing = false;
        }

        private async void UpdateAdornment(bool highlight, ITableEntryHandle[] entries)
        {
            var errorResults = GetErrors(entries);
            _text.SetValues(errorResults.Errors, errorResults.Warnings, errorResults.Info);

            if (highlight)
                await _text.HighlightAsync();
        }

        private ErrorResult GetErrors(ITableEntryHandle[] entries)
        {
            var errorResult = new ErrorResult();
            try
            {
                __VSERRORCATEGORY val = default;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (!TableEntryExtensions.TryGetValue((ITableEntry)(object)entries[i], "errorseverity", out val))
                        val = (__VSERRORCATEGORY)2;

                    if (val != 0)
                    {
                        if ((int)val == 1)
                        {
                            errorResult.Warnings++;
                        }
                        else
                        {
                            errorResult.Info++;
                        }
                    }
                    else
                    {
                        errorResult.Errors++;
                    }
                }

                return errorResult;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void TableControl_EntriesChanged(object sender, EntriesChangedEventArgs e)
        {
            Task.Run(() =>
            {
                _dispatcher.Invoke(new Action(() =>
                {
                    Update(false);
                }), DispatcherPriority.ApplicationIdle, null);
            });
        }

        private void SetAdornmentLocation(object sender, EventArgs e)
        {
            Canvas.SetLeft(_text, _view.ViewportRight - 130);
            Canvas.SetTop(_text, _view.ViewportTop + 20);
        }

        private void text_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = (DTE)_serviceProvider.GetService(typeof(DTE));
            if (dte != null)
                dte.ExecuteCommand("View.ErrorList");
        }

        public void Dispose()
        {
            if (_view != null)
            {
                _view.ViewportHeightChanged -= SetAdornmentLocation;
                _view.ViewportWidthChanged -= SetAdornmentLocation;
            }

            if (_text != null)
                _text.MouseUp -= text_MouseUp;

            if (_errorList != null)
                _errorList.TableControl.EntriesChanged -= TableControl_EntriesChanged;
        }
    }
}