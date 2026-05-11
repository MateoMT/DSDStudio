using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSDCore;
using IDE.Services;
using IDE.ViewModels.Base;

using Microsoft.Win32;
using ODE;
using OxyPlot;
using PaintUtils;
using SkiaSharp;

namespace IDE.ViewModels
{
    public partial class ResultViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IFileService _fileService;
        private readonly IDSDCoreService _dsdCoreService;

        private List<SvgGenerator> _svgs;
        private ODEsys _odes;
        private List<double[]> _simulationData;
        private List<double> _time;
        private double left;
        private double top;
        private double width;
        private double height;

        [ObservableProperty]
        private WindowState _windowState = WindowState.Normal;

        [ObservableProperty]
        private object _currentPage;

        [ObservableProperty]
        private ReactivePageViewModel _reactivePage;

        [ObservableProperty]
        private ODEPageViewModel _odePage;

        [ObservableProperty]
        private TablePageViewModel _tablePage;

        [ObservableProperty]
        private ChartPageViewModel _chartPage;

        public ResultViewModel(INavigationService navigationService, IFileService fileService, List<SvgGenerator> svgs, ODEsys odes, List<double[]> datas)
        {
            _navigationService = navigationService;
            _fileService = fileService;
            _svgs = svgs;
            _odes = odes;
            _simulationData = datas;
            InitializePages();
            CurrentPage = ReactivePage;
        }
        public ResultViewModel(INavigationService navigationService, IFileService fileService, List<SvgGenerator> svgs, ODEsys odes, List<double[]> datas, List<double> time)
        {
            _navigationService = navigationService;
            _fileService = fileService;
            _svgs = svgs;
            _odes = odes;
            _time = time;
            _simulationData = datas;
            InitializePages();
            CurrentPage = ReactivePage;
        }
        public ResultViewModel(INavigationService navigationService, IFileService fileService, IDSDCoreService dsdCoreService)
        {
            _navigationService = navigationService;
            _fileService = fileService;
            _dsdCoreService = dsdCoreService;

            InitializePages();
            CurrentPage = ReactivePage;
        }

        public WindowState GetState()
        {
            return WindowState;
        }
        public void setData(List<double[]> datas, List<double> time)
        {
            if (datas == null)
                return;
            _simulationData = datas;
            ChartPage = new ChartPageViewModel(_simulationData, time, _odes);
        }
        private void InitializePages()
        {
            ReactivePage = new ReactivePageViewModel(_svgs);

            OdePage = new ODEPageViewModel(_odes);
            if (_simulationData != null && _time != null)
                ChartPage = new ChartPageViewModel(_simulationData, _time, _odes);
            TablePage = new();
        }

        [RelayCommand]
        private void ShowReactivePage()
        {
            CurrentPage = ReactivePage;
        }

        [RelayCommand]
        private void ShowODEPage()
        {
            CurrentPage = OdePage;
        }

        [RelayCommand]
        private void ShowTablePage()
        {
            if (_simulationData == null)
            {
                MessageBox.Show("Please run a simulation before showing the data table.");
                return;
            }
            CurrentPage = TablePage;

        }

        [RelayCommand]
        private void ShowChartPage()
        {
            if (_simulationData == null)
            {
                MessageBox.Show("Please run a simulation before showing the chart.");
                return;
            }
            CurrentPage = ChartPage;
        }

        [RelayCommand]
        private void SaveToFile()
        {
            string filter = string.Empty;
            string defaultExt = string.Empty;
            byte[] fileContent = null;

            if (CurrentPage == ReactivePage)
            {
                filter = "SVG files (*.svg)|*.svg";
                defaultExt = ".svg";
                bool saveAllSvgs = false;

                if (CurrentPage == ReactivePage && ReactivePage.IsAllViewMode)
                {
                    saveAllSvgs = true;
                }
                if (!saveAllSvgs)
                    fileContent = ReactivePage.GetSelectedSvgContent();
                else
                    fileContent = ReactivePage.GetAllSvgs();
            }
            else if (CurrentPage == OdePage)
            {
                filter = "LaTeX files (*.tex)|*.tex";
                defaultExt = ".tex";
                fileContent = System.Text.Encoding.UTF8.GetBytes(OdePage.GetLaTeXContent());
            }
            else if (CurrentPage == TablePage)
            {

                Window resultWindow = null;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window.DataContext == this)
                        {
                            resultWindow = window;
                            break;
                        }
                    }
                });
                if (resultWindow != null)
                {
                    var result = resultWindow as Views.Results;
                    var ReoGrid = result.GetReo();
                    SaveFileDialog saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx",
                        Title = "Export to Excel"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        ReoGrid.Save(saveFileDialog.FileName, unvell.ReoGrid.IO.FileFormat.Excel2007);
                        MessageBox.Show($"File saved to: {saveFileDialog.FileName}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                return;

            }
            else if (CurrentPage == ChartPage)
            {
                filter = "SVG files (*.svg)|*.svg";
                defaultExt = ".svg";
                fileContent = ChartPage.GetChartImage();
            }

            if (fileContent != null)
            {
                string filePath = _fileService.SaveFileDialog(filter);
                if (!string.IsNullOrEmpty(filePath))
                {
                    try
                    {
                        File.WriteAllBytes(filePath, fileContent);
                        MessageBox.Show($"File saved to: {filePath}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving file: {ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        [RelayCommand]
        private void Minimize()
        {
            Window resultWindow = null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.DataContext == this)
                    {
                        resultWindow = window;
                        break;
                    }
                }
            });
            if (resultWindow != null)
            {
                resultWindow.WindowState = WindowState.Minimized;
            }
        }

        [RelayCommand]
        private void ToggleMaximize()
        {
            Window resultWindow = null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.DataContext == this)
                    {
                        resultWindow = window;
                        break;
                    }
                }
            });

            if (resultWindow != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    resultWindow.WindowState = WindowState.Normal;
                    WindowState = WindowState.Normal;
                    resultWindow.Left = left;
                    resultWindow.Top = top;
                    resultWindow.Width = width;
                    resultWindow.Height = height;
                }
                else
                {
                    resultWindow.WindowState = WindowState.Normal;
                    left = resultWindow.Left;
                    top = resultWindow.Top;
                    width = resultWindow.Width;
                    height = resultWindow.Height;
                    resultWindow.Left = SystemParameters.WorkArea.Left;
                    resultWindow.Top = SystemParameters.WorkArea.Top;
                    resultWindow.Width = SystemParameters.WorkArea.Width;
                    resultWindow.Height = SystemParameters.WorkArea.Height;
                    resultWindow.WindowState = WindowState.Normal;
                    WindowState = WindowState.Maximized;
                }
            }
        }

        [RelayCommand]
        private void Close()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.DataContext == this)
                    {
                        window.Close();
                        Application.Current.MainWindow.Activate();
                        break;
                    }
                }
            });
        }

    }

    public class ReactivePageViewModel : ObservableObject
    {
        public ObservableCollection<SvgViewModel> SvgItems { get; }
        private List<SvgGenerator> svgGenerators;

        private SvgViewModel _selectedSvg;
        public SvgViewModel SelectedSvg
        {
            get => _selectedSvg;
            set => SetProperty(ref _selectedSvg, value);
        }
        private bool _isAllViewMode = true;
        public bool IsAllViewMode
        {
            get => _isAllViewMode;
            set => SetProperty(ref _isAllViewMode, value);
        }

        public byte[] GetAllSvgs()
        {
            double width = 0, height = 0;
            foreach (var svg in svgGenerators)
            {
                width = Math.Max(width, svg.width);
                height += svg.height;
            }
            SvgGenerator svgGenerator = new SvgGenerator(width, height);
            double currentHeight = 0;
            foreach (var svg in svgGenerators)
            {
                svgGenerator.AddSubSvg(svg, 0, currentHeight);
                currentHeight += svg.height;
            }
            string svgContent = svgGenerator.getSvgString();
            if (!string.IsNullOrEmpty(svgContent))
            {
                return System.Text.Encoding.UTF8.GetBytes(svgContent);
            }
            return null;
        }
        public ReactivePageViewModel(List<SvgGenerator> svgs)
        {
            SvgItems = new ObservableCollection<SvgViewModel>();
            svgGenerators = svgs;
            if (svgs != null && svgs.Count > 0)
            {
                for (int i = 0; i < svgs.Count; i++)
                {
                    SvgGenerator svg = svgs[i];
                    string svgContent = svg.getSvgString();
                    if (!string.IsNullOrEmpty(svgContent))
                    {
                        SvgItems.Add(new SvgViewModel { SvgContent = svgContent, Index = i });
                    }
                }
            }

            if (SvgItems.Count > 0)
            {
                SelectedSvg = SvgItems[0];
            }
        }

        public byte[] GetSelectedSvgContent()
        {
            if (SelectedSvg != null && !string.IsNullOrEmpty(SelectedSvg.SvgContent))
            {
                return System.Text.Encoding.UTF8.GetBytes(SelectedSvg.SvgContent);
            }
            return null;
        }
    }

    public class SvgViewModel : ObservableObject
    {
        private string _svgContent;
        private int _index;

        public string SvgContent
        {
            get => _svgContent;
            set => SetProperty(ref _svgContent, value);
        }

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }
    }

    public class ODEPageViewModel
    {
        public string LaTeXContent { get; set; }

        public ODEPageViewModel(ODEsys odeSystem)
        {
            if (odeSystem != null)
            {
                LaTeXContent = odeSystem.ToLatex();
            }
            else
            {
                LaTeXContent = @"\text{No differential equation data available}";
            }
        }

        public string GetLaTeXContent()
        {
            return LaTeXContent;
        }
    }

    public class TablePageViewModel
    {
        public ObservableCollection<Dictionary<string, object>> TableData { get; }
        public TablePageViewModel()
        {

        }

    }

    public class ConcentrationData
    {
        public double Time { get; set; }
        public ObservableCollection<SpeciesConcentration> SpeciesConcentrations { get; } = new ObservableCollection<SpeciesConcentration>();
    }

    public class SpeciesConcentration
    {
        public string Name { get; set; }
        public double Concentration { get; set; }
    }

    public class ChartPageViewModel : ObservableObject
    {
        private PlotModel _plotModel;
        public PlotModel PlotModel
        {
            get => _plotModel;
            set => SetProperty(ref _plotModel, value);
        }

        private int _maxDataPoints = 10000;
        public int MaxDataPoints
        {
            get => _maxDataPoints;
            set => SetProperty(ref _maxDataPoints, value);
        }
        private static string GetAvailableFontName(string preferredFont, string fallbackFont)
        {
            foreach (var fontFamily in Fonts.SystemFontFamilies)
            {
                if (fontFamily.Source.Equals(preferredFont, StringComparison.OrdinalIgnoreCase))
                {
                    return preferredFont;
                }
            }

            return fallbackFont;
        }
        public ChartPageViewModel(List<double[]> simulationData, List<double> time, ODEsys odeSystem)
        {
            SimulationData = simulationData;

            PlotModel = new PlotModel
            {
                Title = "DNA Strand Displacement Simulation Results",
                TitleFontSize = 30,
                DefaultFont = GetAvailableFontName("LXGW Bright Code", "SimSun"),
                Background = OxyColors.White,
                DefaultFontSize = 20,
                EdgeRenderingMode = EdgeRenderingMode.PreferGeometricAccuracy
            };
            var legend = new OxyPlot.Legends.Legend
            {
                LegendPosition = OxyPlot.Legends.LegendPosition.RightTop,
                LegendOrientation = OxyPlot.Legends.LegendOrientation.Vertical,
                LegendTextColor = OxyColors.Black,
                LegendFontSize = 25,
                LegendBackground = OxyColor.FromAColor(200, OxyColors.White),
                LegendBorder = OxyColors.Gray,
                LegendBorderThickness = 1,
                LegendPlacement = OxyPlot.Legends.LegendPlacement.Inside,
                LegendMargin = 10
            };
            PlotModel.Legends.Add(legend);
            PlotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Time",
                TitleFontSize = 25,
                TitleFontWeight = OxyPlot.FontWeights.Bold,
                Minimum = time?.FirstOrDefault() ?? 0,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            PlotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Concentration",
                TitleFontSize = 25,
                TitleFontWeight = OxyPlot.FontWeights.Bold,
                Minimum = 0,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            if (simulationData != null && simulationData.Count > 0 && odeSystem != null)
            {
                SpeciesNames = new List<string>();
                foreach (var name in odeSystem.names)
                {
                    SpeciesNames.Add(name.Value);
                }

                var colors = new List<OxyColor>
                {
                    OxyColors.Red, OxyColors.Blue, OxyColors.Green,
                    OxyColors.Orange, OxyColors.Purple, OxyColors.Cyan,
                    OxyColors.Magenta, OxyColors.Brown, OxyColors.Teal,
                    OxyColors.Pink, OxyColors.Olive, OxyColors.Navy
                };
                var sampledIndices = SampleDataIndices(simulationData.Count);

                for (int j = 0; j < SpeciesNames.Count; j++)
                {
                    var lineSeries = new OxyPlot.Series.LineSeries
                    {
                        Title = SpeciesNames[j],
                        Color = colors[j % colors.Count],
                        StrokeThickness = 4,
                        MarkerType = MarkerType.None,
                        MarkerSize = 3,
                        InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline,
                    };
                    foreach (var i in sampledIndices)
                    {
                        lineSeries.Points.Add(new DataPoint((double)time[i] / 1.0, simulationData[i][j]));
                    }

                    PlotModel.Series.Add(lineSeries);
                }
            }
        }

        private List<int> SampleDataIndices(int dataCount)
        {
            if (dataCount <= MaxDataPoints)
            {
                return Enumerable.Range(0, dataCount).ToList();
            }

            var result = new List<int>();

            result.Add(0);
            result.Add(dataCount - 1);
            int step = dataCount / MaxDataPoints;
            if (step < 1) step = 1;
            for (int i = step; i < dataCount - 1; i += step)
            {
                result.Add(i);
            }

            return result.Distinct().OrderBy(i => i).ToList();
        }

        public List<double[]> SimulationData { get; }
        public List<string> SpeciesNames { get; }

        public byte[] GetChartImage()
        {
            var pngExporter = new OxyPlot.SkiaSharp.SvgExporter
            {
                Width = 800,
                Height = 600,
            };
            using (var stream = new MemoryStream())
            {
                pngExporter.Export(PlotModel, stream);
                return stream.ToArray();
            }
        }
    }
}
