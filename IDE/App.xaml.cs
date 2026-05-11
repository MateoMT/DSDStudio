using System.Configuration;
using System.Data;
using System.Windows;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using IDE.Services;
using IDE.ViewModels;
using IDE.Views;

using SkiaSharp;
using System.Xml;
using System.IO;
using System.Reflection;

namespace IDE
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool DirectFileOpen { get; private set; } = false;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            var navigationService = (INavigationService)Application.Current.Resources["NavigationService"];

            
            //RegisterFileAssociation();
            string filePath = null;
            if (e.Args.Length > 0)
            {
                filePath = e.Args[0];
                if (File.Exists(filePath))
                {
                    RegisterHighlighting();
                    //MessageBox.Show(filePath);
                    DirectFileOpen = true;
                    var mainViewModel = new IDEMainViewModel(navigationService, new FileService(), new DSDCoreService(), filePath);
                    var mainWindow = new IDEMain(mainViewModel);

                    Application.Current.MainWindow = mainWindow;
                    Application.Current.MainWindow.Show();
                    //MessageBox.Show(filePath);
                    return;
                }
            }
            var initViewModel = new InitViewModel(navigationService);
            var initWindow = new Init
            {
                DataContext = initViewModel
            };

            Application.Current.MainWindow = initWindow;
            Application.Current.MainWindow.Show();

            RegisterHighlighting();
        }
        private void RegisterHighlighting()
        {
            try
            {

                IHighlightingDefinition customHighlighting;
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("IDE.Themes.dsd.xshd"))
                {
                    using (XmlReader reader = new XmlTextReader(s))
                    {
                        customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
                // 注册语法高亮
                HighlightingManager.Instance.RegisterHighlighting("DSD", new[] { ".dsd" }, customHighlighting);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载语法高亮定义时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 注册文件关联 需要管理员
        public static void RegisterFileAssociation()
        {
            try
            {
                string executablePath = Assembly.GetExecutingAssembly().Location;
                string fileType = ".dsd";
                string fileTypeDescription = "DSD 文件";
                string programId = "DSDIdeApp";

                using (var fileTypeKey = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(fileType))
                {
                    fileTypeKey.SetValue("", programId);
                }

                // 注册程序ID和描述
                using (var progIdKey = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(programId))
                {
                    progIdKey.SetValue("", fileTypeDescription);

                    // 设置默认图标
                    using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
                    {
                        iconKey.SetValue("", $"{executablePath},0");
                    }

                    // 设置打开命令
                    using (var shellKey = progIdKey.CreateSubKey("shell"))
                    using (var openKey = shellKey.CreateSubKey("open"))
                    using (var commandKey = openKey.CreateSubKey("command"))
                    {
                        commandKey.SetValue("", $"\"{executablePath}\" \"%1\"");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("注册文件关联失败，请以管理员身份运行程序。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"注册文件关联时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

}
