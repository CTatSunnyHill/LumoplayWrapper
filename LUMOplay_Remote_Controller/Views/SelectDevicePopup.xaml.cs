using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.ViewModels;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Views
{
    public partial class SelectDevicePopup : ContentPage
    {
        public SelectDevicePopup(IEnumerable<LumoplayDevice> devices)
        {
            InitializeComponent();
            BindingContext = new SelectDevicePopupViewModel(devices);
        }

        public SelectDevicePopupViewModel ViewModel => (SelectDevicePopupViewModel)BindingContext;
    }
}