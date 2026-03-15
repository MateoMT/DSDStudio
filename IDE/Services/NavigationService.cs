using System.Windows;
using System.Windows.Threading;
using IDE.ViewModels;
using IDE.Views;
using ODE;
using PaintUtils;

namespace IDE.Services
{
    public interface INavigationService
    {
        void NavigateToSelect();
        void NavigateToIDEMain(string filePath = null);
        void CloseCurrentWindow();
        Window NavigateToResults(List<SvgGenerator> svgs, ODEsys odes, List<double[]> doubles = null);
        void CloseAllWindows();
        Window NavigateToAbout();
    }
    public class NavigationService : INavigationService
    {


        public void NavigateToSelect()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var currentWindow = Application.Current.MainWindow;
                var selectViewModel = new SelectViewModel(this, new FileService());
                var selectWindow = new Views.Select
                {
                    DataContext = selectViewModel
                };
                Application.Current.MainWindow = selectWindow;
                
                selectWindow.Show();
                currentWindow.Close();

            });
        }
        public void  NavigateToIDEMain(string filePath = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var currentWindow = Application.Current.MainWindow;
                var ideMainWindowViewModel = filePath == null
                ? new IDEMainViewModel(this, new FileService(),new DSDCoreService())
                : new IDEMainViewModel(this, new FileService(), new DSDCoreService(), filePath);

                var ideMainWindow = new Views.IDEMain(ideMainWindowViewModel);

                Application.Current.MainWindow = ideMainWindow;
                ideMainWindow.Show();
                currentWindow.Close();

            });
        }

        public Window NavigateToResults(List<SvgGenerator> svgs, ODEsys odes, List<double[]> doubles = null)
        {
            Window resultsWindow = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var ResultsViewModel = new ResultViewModel(this, new FileService(), svgs, odes, doubles);
                resultsWindow = new Views.Results
                {
                    DataContext = ResultsViewModel,
                    Owner = Application.Current.MainWindow
                };
                resultsWindow.Show();
            });
            return resultsWindow;
        }
        public void CloseWindow(Window window)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (window != null && window.IsVisible)
                {
                    window.Close();
                }
            });
        }
        public void CloseCurrentWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var currentWindow = Application.Current.MainWindow;
                currentWindow.Close();
            });
        }
        public void CloseAllWindows()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 获取应用程序中所有打开的窗口
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != Application.Current.MainWindow)
                    {
                        window.Close();
                    }
                }

                // 最后关闭主窗口
                Application.Current.MainWindow?.Close();
            });
        }
        public Window NavigateToAbout()
        {
            Window aboutWindow = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var aboutViewModel = new AboutViewModel(this);
                aboutWindow = new Views.About(aboutViewModel)
                {
                    Owner = Application.Current.MainWindow
                };
                aboutWindow.Show();
            });
            return aboutWindow;
        }
    }
}
