using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.AvalonEdit.Document;
using IDE.Services;
using IDE.ViewModels.Base;
using IDE.Views;

namespace IDE.ViewModels
{
    public partial class IDEMainViewModel: ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IFileService _fileService;
        private readonly IDSDCoreService _dsdCoreService;

        private Window _lastResultWindow;
        private ResultViewModel _lastResultViewModel;
        private double left;
        private double top;
        private double width;
        private double height;

        public event Action OnCopyRequested;
        public event Action OnPasteRequested;
        public event Action OnCutRequested;
        public event Action<List<ErrorMarker>> OnErrorsFound;

        [ObservableProperty]
        private WindowState _windowState1 = WindowState.Normal;

        [ObservableProperty]
        private string _filePath;

        [ObservableProperty]
        private string _outputMessage = "Ready";

        [ObservableProperty]
        private bool _isWordWrapEnabled = false;

        public class ErrorMarker
        {
            public int Line { get; set; }
            public int Column { get; set; }
            public string Message { get; set; }
        }
        public WindowState GetState()
        {
            return WindowState1;
        }

        [ObservableProperty]
        private List<ErrorMarker> _errorMarkers = new List<ErrorMarker>();

        public string Content
        {
            get => Document.Text;
            set
            {
                if (Document.Text != value)
                {
                    Document.Text = value;
                    OnPropertyChanged();
                }
            }
        }

        [ObservableProperty]
        private TextDocument _document = new TextDocument();

        [ObservableProperty]
        private double _fontSize = 20;

        public IDEMainViewModel(INavigationService navigationService, IFileService fileService, IDSDCoreService dsdCoreService)
        {
            _navigationService = navigationService;
            _fileService = fileService;
            _dsdCoreService = dsdCoreService;
            StatusMessage = "Ready";
        }

        public IDEMainViewModel(INavigationService navigationService, IFileService fileService, IDSDCoreService dsdCoreService, string filePath)
            : this(navigationService, fileService, dsdCoreService)
        {
            LoadFileContent(filePath);
        }

        public IDEMainViewModel()
        {
            _navigationService = (NavigationService)Application.Current.Resources["NavigationService"];
            _fileService = (FileService)Application.Current.Resources["FileService"];
            _dsdCoreService = (DSDCoreService)Application.Current.Resources["DSDCoreService"];
        }

        [RelayCommand]
        private void NewFile()
        {
            if(Document.Text!=null)
            {
                var result = MessageBox.Show("Save the current file?", "Prompt", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Save();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }
            FilePath = null;
            Document.Text = string.Empty;
            OutputMessage += "\nNew file created";
        }

        [RelayCommand]
        private void Open()
        {
            var path = _fileService.OpenFileDialog("DSD Files (*.dsd)|*.dsd");
            if (!string.IsNullOrEmpty(path))
            {
                LoadFileContent(path);
            }
        }

        [RelayCommand]
        private void Save()
        {
            StatusMessage = "Saving file";
            OutputMessage += $"\nSaving file {FilePath}";
            if (string.IsNullOrEmpty(FilePath))
            {
                SaveAs();
                return;
            }

            try
            {
                File.WriteAllText(FilePath, Content);
                OutputMessage += $"\nFile saved: {FilePath}";
            }
            catch (Exception ex)
            {
                OutputMessage += $"\nSave failed: {ex.Message}";
            }
            StatusMessage = "Ready";
        }

        [RelayCommand]
        private void SaveAs()
        {
            var path = _fileService.SaveFileDialog("DSD Files (*.dsd)|*.dsd");
            if (!string.IsNullOrEmpty(path))
            {
                FilePath = path;
                Save();
            }
            else
            {
                OutputMessage += "\nSave failed: No file path selected";
            }
            StatusMessage = "Ready";
        }

        [RelayCommand]
        private void Close()
        {
            if(Document.Text != null)
            {
                if(Document.Text.Length == 0)
                {
                    _navigationService.CloseAllWindows();
                    return;
                }
                if (FilePath!=null)
                {
                    try
                    {
                        var file = File.ReadAllText(FilePath);
                        if(file.Equals(Document.Text))
                        {
                            _navigationService.CloseAllWindows();
                            return;
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
                var result = MessageBox.Show("Save the current file?", "Prompt", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Save();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }
            _navigationService.CloseAllWindows();
        }

        [RelayCommand]
        private static void Exit()
        {
            Application.Current.Shutdown();
        }

        [RelayCommand]
        private void Minimize()
        {
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        [RelayCommand]
        private void ToggleMaximize()
        {
            var window = Application.Current.MainWindow;
            if (WindowState1 == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;
                WindowState1 = WindowState.Normal;
                window.Left = left;
                window.Top = top;
                window.Width = width;
                window.Height = height;

            }
            else
            {        
                window.WindowState = WindowState.Normal;
                left = window.Left;
                top = window.Top;
                width = window.Width;
                height = window.Height;
                window.Left = SystemParameters.WorkArea.Left;
                window.Top = SystemParameters.WorkArea.Top;
                window.Width = SystemParameters.WorkArea.Width;
                window.Height = SystemParameters.WorkArea.Height;
                window.WindowState = WindowState.Normal; 
                WindowState1 = WindowState.Maximized;
            }
        }

        private void LoadFileContent(string path)
        {
            StatusMessage = "Loading file";
            try
            {
                FilePath = path;
                Document.Text = File.ReadAllText(path);
                OutputMessage += $"\nFile loaded: {path}";
            }
            catch (Exception ex)
            {
                OutputMessage += $"\nLoad failed: {ex.Message}";
            }
            StatusMessage = "Ready";
        }

        [RelayCommand]
        public void ZoomIn()
        {
            FontSize += 1;
        }

        [RelayCommand]
        public void ZoomOut()
        {
            if (FontSize > 10) FontSize -= 1;
        }

        [RelayCommand]
        private void Copy()
        {
            OnCopyRequested?.Invoke();
        }

        [RelayCommand]
        private void Paste()
        {
            OnPasteRequested?.Invoke();
        }

        [RelayCommand]
        private void Cut()
        {
            OnCutRequested?.Invoke();
        }

        private bool comiple(bool flag = true)
        {
            StatusMessage = "Compiling...";
            OutputMessage += "\nCompilation started...";
            if (FilePath == null)
            {
                var result = MessageBox.Show("Save the current file?", "Prompt", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Save();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return false;
                }
            }
            else Save();
            ClearErrors();
            if (Content == null || Content.Length == 0)
            {
                OutputMessage += "\nCompilation failed: Content is empty";
                StatusMessage = "Compilation failed";
                return false;
            }
            try
            {
                var (success, errorMsg) = _dsdCoreService.Compile(Content);
                if (success)
                {
                    OutputMessage += "\nCompilation succeeded";
                    StatusMessage = "Compilation succeeded";
                    if(flag)
                    {
                        _lastResultWindow = _navigationService.NavigateToResults(_dsdCoreService.GetReactionSvgs(), _dsdCoreService.GetODESystem());
                        _lastResultViewModel = (ResultViewModel)_lastResultWindow.DataContext;
                    }
                    return true;
                }
                else
                {
                    OutputMessage += $"\nCompilation failed: \n{errorMsg}";
                    StatusMessage = "Compilation failed";
                    ParseErrorsAndHighlight(errorMsg);
                }
            }
            catch (Exception ex)
            {
                OutputMessage += $"\nCompilation error: {ex.Message}";
                StatusMessage = "Compilation error";
                ParseErrorsAndHighlight(ex.Message);
            }
            return false;
        }

        [RelayCommand]
        private void Compile()
        {
            comiple();
        }

        private void ParseErrorsAndHighlight(string errorMsg)
        {
            if (string.IsNullOrWhiteSpace(errorMsg))
                return;

            var errorRegex = new Regex(@"Line\s+(\d+),\s+Column\s+(\d+):\s+(.+)");
            var errorsList = new List<ErrorMarker>();

            foreach (var line in errorMsg.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = errorRegex.Match(line);
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out int lineNumber) &&
                        int.TryParse(match.Groups[2].Value, out int column))
                    {
                        var errorMarker = new ErrorMarker
                        {
                            Line = lineNumber,
                            Column = column,
                            Message = match.Groups[3].Value.Trim()
                        };

                        errorsList.Add(errorMarker);
                    }
                }
            }

            ErrorMarkers = errorsList;
            OnErrorsFound?.Invoke(errorsList);
        }

        private void ClearErrors()
        {
            ErrorMarkers = new List<ErrorMarker>();
            OnErrorsFound.Invoke(new List<ErrorMarker>());
        }

        [RelayCommand]
        private async void Simulate()
        {
            bool sucess = true;
            if (_lastResultWindow == null || _lastResultViewModel == null || !
                            _lastResultWindow.IsVisible)
            {
                sucess = comiple(false);
            }
            if (sucess)
            {
                OutputMessage += "\nSimulation started";
                StatusMessage = "Simulation started";

                char[] spinChars = new[] { '|', '/', '-', '\\' };
                int spinIndex = 0;

                using var cts = new CancellationTokenSource();

                var animationTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                string[] lines = OutputMessage.Split('\n');
                                if (lines.Length > 0 && lines[lines.Length - 1].StartsWith("Simulating"))
                                {
                                    lines[lines.Length - 1] = $"Simulating {spinChars[spinIndex]}";
                                }
                                else
                                {
                                    OutputMessage += $"\nSimulating {spinChars[spinIndex]}";
                                }

                                OutputMessage = string.Join('\n', lines);
                            });

                            spinIndex = (spinIndex + 1) % spinChars.Length;

                            await Task.Delay(200, cts.Token);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                    }
                }, cts.Token);

                try
                {
                    var simulationResult = await Task.Run(() => _dsdCoreService.SimulateWithTime());

                    var (time, simulationData) = simulationResult;

                    if (simulationData != null)
                    {
                        if (_lastResultWindow != null && _lastResultViewModel != null &&
                            _lastResultWindow.IsVisible)
                        {
                            _lastResultWindow.Hide();
                            StatusMessage = "Transferring data";
                            _lastResultViewModel.setData(simulationData, time);
                            var lastwindow = _lastResultWindow as Results;
                            lastwindow.setGrid(simulationData, time, _dsdCoreService.GetODESystem());
                            OutputMessage += "\nSimulation data sent to results window";
                            StatusMessage = "Ready";
                        }
                        else
                        {
                            _lastResultWindow = _navigationService.NavigateToResults(
                                _dsdCoreService.GetReactionSvgs(),
                                _dsdCoreService.GetODESystem(),
                                simulationData);
                            _lastResultWindow.Hide();
                            _lastResultViewModel = (ResultViewModel)_lastResultWindow.DataContext;
                            _lastResultViewModel.setData(simulationData, time);

                            var lastwindow = _lastResultWindow as Results;
                            StatusMessage = "Transferring data";
                            lastwindow.setGrid(simulationData, time, _dsdCoreService.GetODESystem());
                            OutputMessage += "\nOpened new results window to display simulation data";
                            StatusMessage = "Ready";
                        }
                        _lastResultWindow.Show();
                        cts.Cancel();
                        try { await animationTask; } catch { }
                        MessageBox.Show("Simulation completed", "Prompt", MessageBoxButton.OK, MessageBoxImage.Information);

                    }
                    else
                    {
                        string[] lines = OutputMessage.Split('\n');
                        if (lines.Length > 0 && lines[lines.Length - 1].StartsWith("Simulating"))
                        {
                            lines[lines.Length - 1] = $"Simulation failed: {_dsdCoreService.GetErrorMessage()}";
                            OutputMessage = string.Join('\n', lines);
                        }
                        else
                        {
                            OutputMessage += $"\nSimulation failed: {_dsdCoreService.GetErrorMessage()}";
                        }

                        StatusMessage = "Simulation failed";
                    }
                }
                catch (Exception ex)
                {
                    cts.Cancel();
                    try { await animationTask; } catch { }

                    string[] lines = OutputMessage.Split('\n');
                    if (lines.Length > 0 && lines[lines.Length - 1].StartsWith("Simulating"))
                    {
                        lines[lines.Length - 1] = $"Simulation error: {ex.Message}";
                        OutputMessage = string.Join('\n', lines);
                    }
                    else
                    {
                        OutputMessage += $"\nSimulation error: {ex.Message}";
                    }

                    StatusMessage = "Simulation error";
                }
            }
        }

        [RelayCommand]
        private void ShowAbout()
        {
            _navigationService.NavigateToAbout();
        }
    }
}
