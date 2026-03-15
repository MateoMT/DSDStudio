using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using IDE.Services;
using IDE.ViewModels;

namespace IDE.Views
{
    /// <summary>
    /// IDEMain.xaml 的交互逻辑
    /// </summary>
    public partial class IDEMain : Window
    {
        private  ErrorTextMarkerService _errorMarkerService;

        public IDEMain()
        {
            InitializeComponent();


            this.Loaded += (s, e) =>
            {
                _errorMarkerService = new ErrorTextMarkerService(textEditor.Document);
                textEditor.TextArea.TextView.BackgroundRenderers.Add(_errorMarkerService);
                textEditor.TextArea.TextView.LineTransformers.Add(_errorMarkerService);
            };

            var navigationService = (INavigationService)Application.Current.Resources["NavigationService"];
            var fileService = (IFileService)Application.Current.Resources["FileService"];
            var dsdCoreService = (IDSDCoreService)Application.Current.Resources["DSDCoreService"];
            var iDEMainViewModel = new IDEMainViewModel(navigationService, fileService, dsdCoreService);

            iDEMainViewModel.OnCopyRequested += () => textEditor.Copy();
            iDEMainViewModel.OnPasteRequested += () => textEditor.Paste();
            iDEMainViewModel.OnCutRequested += () => textEditor.Cut();
            iDEMainViewModel.OnErrorsFound += HighlightErrors;

            this.DataContext = iDEMainViewModel;
        }
        public IDEMain(IDEMainViewModel iDEMainViewModel)
        {
            InitializeComponent();

            
            this.Loaded += (s, e) =>
            {
                _errorMarkerService = new ErrorTextMarkerService(textEditor.Document);
                textEditor.TextArea.TextView.BackgroundRenderers.Add(_errorMarkerService);
                textEditor.TextArea.TextView.LineTransformers.Add(_errorMarkerService);
            };

            var navigationService = (INavigationService)Application.Current.Resources["NavigationService"];
            var fileService = (IFileService)Application.Current.Resources["FileService"];
            var dsdCoreService = (IDSDCoreService)Application.Current.Resources["DSDCoreService"];

            iDEMainViewModel.OnCopyRequested += () => textEditor.Copy();
            iDEMainViewModel.OnPasteRequested += () => textEditor.Paste();
            iDEMainViewModel.OnCutRequested += () => textEditor.Cut();
            iDEMainViewModel.OnErrorsFound += HighlightErrors;

            this.DataContext = iDEMainViewModel;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.ResizeMode = ResizeMode.NoResize;
            var vm = DataContext as IDEMainViewModel;
            if ( vm.GetState()!= WindowState.Maximized)
                this.DragMove();
        }

        private void TextEditor_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                    ((IDEMainViewModel)DataContext).ZoomIn();
                else
                    ((IDEMainViewModel)DataContext).ZoomOut();
            }
        }

        private void TextEditor_TextChanged(object sender, EventArgs e)
        {
            if (DataContext is IDEMainViewModel viewModel)
            {
                viewModel.Content = textEditor.Document.Text;
            }
        }

        private void outputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            outputTextBox.ScrollToEnd();
        }

        private void HighlightErrors(List<IDEMainViewModel.ErrorMarker> errorMarkers)
        {
            _errorMarkerService.ClearMarkers();

            if (errorMarkers.Count > 0)
            {
                foreach (var error in errorMarkers)
                {
                    int lineNumber = error.Line;
                    if (lineNumber <= 0) lineNumber = 1;

                    if (lineNumber <= textEditor.Document.LineCount)
                    {
                        DocumentLine docLine = textEditor.Document.GetLineByNumber(lineNumber);
                        _errorMarkerService.AddMarker(docLine, error.Message);
                    }
                }

                MessageBox.Show("编译失败: \n" + string.Join("\n", errorMarkers.ConvertAll(e => $"行 {e.Line}, 列 {e.Column}: {e.Message}")),
                                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            textEditor.TextArea.TextView.InvalidateVisual();
            textEditor.TextArea.TextView.Redraw();
        }
    }

    public class ErrorTextMarkerService : IBackgroundRenderer, IVisualLineTransformer
    {
        private readonly TextDocument _document = new TextDocument();
        private readonly List<TextMarker> _markers = new List<TextMarker>();

        public ErrorTextMarkerService(TextDocument document)
        {
            if (document != null)
                _document = document;
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null || !textView.VisualLinesValid)
                return;

            foreach (var marker in _markers)
            {
                if (marker.StartOffset < 0 || marker.StartOffset >= _document.TextLength)
                    continue; // 跳过无效 offset
                var line = _document.GetLineByOffset(marker.StartOffset);
                var visualLine = textView.GetVisualLine(line.LineNumber);
                if (visualLine == null)
                    continue;

                var startPoint = new Point(0, visualLine.VisualTop);
                var endPoint = new Point(textView.ActualWidth, visualLine.VisualTop + visualLine.Height);
                var rect = new Rect(startPoint, endPoint);

                var brush = new SolidColorBrush(Color.FromArgb(80, 255, 80, 80));
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(220, 20, 60)), 1);
                drawingContext.DrawRectangle(brush, pen, rect);
            }
        }

        public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
        {
        }

        public void AddMarker(DocumentLine line, string message)
        {
            if (line == null) return;

            var marker = new TextMarker(line.Offset, line.Length)
            {
                ToolTip = message
            };
            _markers.Add(marker);
        }

        public void ClearMarkers()
        {
            _markers.Clear();
        }

        private class TextMarker
        {
            public TextMarker(int startOffset, int length)
            {
                StartOffset = startOffset;
                Length = length;
            }

            public int StartOffset { get; }
            public int Length { get; }
            public string ToolTip { get; set; }
        }
    }
}
