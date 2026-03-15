using IDE.Services;
using IDE.ViewModels.Base;

namespace IDE.ViewModels
{
    internal class InitViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;

        public InitViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            LoadConfigurationAsync();
        }

        private async void LoadConfigurationAsync()
        {
            if (IDE.App.DirectFileOpen)
                return;
            IsBusy = true;
            StatusMessage = "Loading configuration...";
            await Task.Delay(2000);

            StatusMessage = "Configuration loaded";
            _navigationService.NavigateToSelect();
        }
    }
}
