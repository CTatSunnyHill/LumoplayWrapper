using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.ViewModels;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Views
{
    /**
     * Browsable catalogue of games, each with a launch button. Launching needs a
     * target device, so this page owns the modal picker flow rather than the
     * view model, which cannot present UI itself.
     */
    public partial class GameLibraryPage : ContentPage
    {
        /**
         * <param name="viewModel">the page's view model, supplied by DI</param>
         */
        public GameLibraryPage(GameLibraryViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        /**
         * Asks which device to launch on, then hands the choice to the view
         * model. The picker reports its result through a TaskCompletionSource,
         * so this method can await the user's selection; dismissing without
         * choosing yields null and nothing is launched.
         *
         * <param name="sender">the tapped launch button, carrying its game as CommandParameter</param>
         * <param name="e">unused</param>
         */
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
