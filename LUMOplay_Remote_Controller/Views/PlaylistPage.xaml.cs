using LUMOplay_Remote_Controller.Services;
using LUMOplay_Remote_Controller.ViewModels;

namespace LUMOplay_Remote_Controller.Views;

/**
 * Playlist browser and editor. All behaviour lives in
 * <see cref="PlaylistViewModel"/>; this file only wires the two together.
 */
public partial class PlaylistPage : ContentPage
{
	/**
	 * <param name="viewModel">the page's view model, supplied by DI</param>
	 */
	public PlaylistPage(PlaylistViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
