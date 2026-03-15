using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using IDE.Services;
using IDE.ViewModels.Base;

namespace IDE.ViewModels
{
    public partial class SelectViewModel : ViewModelBase
    {
        private readonly FileService _fileService;
        private readonly NavigationService _navigationService;

        public SelectViewModel()
        {
            _navigationService = (NavigationService)Application.Current.Resources["NavigationService"];
            _fileService = (FileService)Application.Current.Resources["FileService"];
        }
        public SelectViewModel(NavigationService navigationService, FileService fileService)
        {
            _navigationService = navigationService;
            _fileService = fileService;
        }
        [RelayCommand]
        private void CreateNewFile()
        {
            _navigationService.NavigateToIDEMain();
        }

        [RelayCommand]
        private void OpenFile()
        {
            string filePath = _fileService.OpenFileDialog("DSD文件 (*.dsd)|*.dsd");

            if (!string.IsNullOrEmpty(filePath))
            {
                _navigationService.NavigateToIDEMain(filePath);
            }
        }

        [RelayCommand]
        private void CloseWindow()
        {
            _navigationService.CloseCurrentWindow();
        }
    }
}
