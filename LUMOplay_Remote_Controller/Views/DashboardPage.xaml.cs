using LUMOplay_Remote_Controller.ViewModels;

namespace LUMOplay_Remote_Controller.Views
{
    public partial class DashboardPage : ContentPage
    {
        public DashboardPage(DashboardViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
