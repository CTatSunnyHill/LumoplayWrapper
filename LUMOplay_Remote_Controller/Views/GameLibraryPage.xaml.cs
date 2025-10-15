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
    }
}