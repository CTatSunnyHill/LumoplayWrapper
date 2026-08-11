using LUMOplay_Remote_Controller.ViewModels;

namespace LUMOplay_Remote_Controller.Views;

/**
 * The main screen: every device with its status and playback controls.
 * All behaviour lives in <see cref="DashboardViewModel"/>; this file only
 * wires the two together.
 */
public partial class DashboardPage : ContentPage
{
	/**
	 * <param name="viewModel">the page's view model, supplied by DI</param>
	 */
	public DashboardPage(DashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
