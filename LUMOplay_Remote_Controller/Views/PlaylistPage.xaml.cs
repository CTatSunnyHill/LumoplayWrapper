using LUMOplay_Remote_Controller.Services;
using LUMOplay_Remote_Controller.ViewModels;

namespace LUMOplay_Remote_Controller.Views;

public partial class PlaylistPage : ContentPage
{
	public PlaylistPage(PlaylistViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}