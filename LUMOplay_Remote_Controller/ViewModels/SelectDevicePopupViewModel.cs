using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUMOplay_Remote_Controller.Model;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace LUMOplay_Remote_Controller.ViewModels
{
    /**
     * Backs the modal device picker. Reports the user's choice through
     * <see cref="CompletionSource"/> rather than an event, so the caller that
     * opened the popup can simply await the result.
     */
    public partial class SelectDevicePopupViewModel : ObservableObject
    {
        /** The devices offered in the list. */
        public ObservableCollection<LumoplayDevice> Devices { get; }

        /** The device the user has highlighted, or null before they pick one. */
        [ObservableProperty]
        private LumoplayDevice selectedDevice;

        /** Confirms the selection; enabled once a device is highlighted. */
        public IRelayCommand PlayCommand { get; }

        /**
         * Completes with the chosen device once the user confirms. The caller
         * awaits this to learn the outcome; it stays pending if the popup is
         * dismissed without confirming.
         */
        public TaskCompletionSource<LumoplayDevice> CompletionSource { get; } = new();

        /**
         * <param name="devices">the devices to offer; copied into an observable collection</param>
         */
        public SelectDevicePopupViewModel(IEnumerable<LumoplayDevice> devices)
        {
            Devices = new ObservableCollection<LumoplayDevice>(devices);
            PlayCommand = new RelayCommand(OnPlay, CanPlay);
        }

        /**
         * Gates <see cref="PlayCommand"/> on a device having been selected.
         *
         * <returns>true when a device is selected</returns>
         */
        private bool CanPlay() => SelectedDevice != null;

        /** Hands the selected device back to whoever is awaiting the popup. */
        private void OnPlay()
        {
            CompletionSource.TrySetResult(SelectedDevice);
        }
    }
}
