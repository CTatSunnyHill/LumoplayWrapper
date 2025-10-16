using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.ViewModels;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Views
{
    public partial class GameLibraryPage : ContentPage
    {
        public GameLibraryPage()
        {
            InitializeComponent();
            BindingContext = new GameLibraryViewModel();
        }

        private async void OnLaunchButtonClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is LumoplayGame game)
            {
                var vm = (GameLibraryViewModel)BindingContext;
                var popup = new SelectDevicePopup(vm.Devices);
                await Navigation.PushModalAsync(popup);

                var selectedDevice = await popup.ViewModel.CompletionSource.Task;
                await Navigation.PopModalAsync();

                if (selectedDevice != null)
                {
                    await vm.LaunchGameAsync(game, selectedDevice);
                }
            }
        }
    }
}