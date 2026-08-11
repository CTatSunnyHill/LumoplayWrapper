using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.ViewModels;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Views
{
    /**
     * Modal device picker shown when launching a game. Its view model is built
     * here rather than injected, because the candidate devices are only known at
     * the moment the popup is opened. The caller awaits the view model's
     * completion source to learn what was chosen.
     */
    public partial class SelectDevicePopup : ContentPage
    {
        /**
         * <param name="devices">the devices to offer</param>
         */
        public SelectDevicePopup(IEnumerable<LumoplayDevice> devices)
        {
            InitializeComponent();
            BindingContext = new SelectDevicePopupViewModel(devices);
        }

        /** The popup's view model, exposed so callers can await its selection. */
        public SelectDevicePopupViewModel ViewModel => (SelectDevicePopupViewModel)BindingContext;
    }
}
