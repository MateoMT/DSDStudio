using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IDE.Services;
using IDE.ViewModels.Base;

namespace IDE.ViewModels
{
    public partial class AboutViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private Window _window;

        [ObservableProperty]
        private string _appTitle = "DSDStudio";

        [ObservableProperty]
        private string _appVersion = "Version 1.0.0";

        [ObservableProperty]
        private string _copyright = "© 2026 ";

        [ObservableProperty]
        private string _description = "DSDStudio is an integrated development environment for modeling and simulating DNA strand displacement systems.";

        public AboutViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        public void SetWindow(Window window)
        {
            _window = window;
        }

        [RelayCommand]
        private void Close()
        {
            _window?.Close();
        }
    }
}
