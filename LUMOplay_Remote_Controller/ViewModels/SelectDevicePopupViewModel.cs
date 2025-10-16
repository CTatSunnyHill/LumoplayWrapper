using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUMOplay_Remote_Controller.Model;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace LUMOplay_Remote_Controller.ViewModels
{
    public partial class SelectDevicePopupViewModel : ObservableObject
    {
        public ObservableCollection<LumoplayDevice> Devices { get; }
        
        [ObservableProperty]
        private LumoplayDevice selectedDevice;

        public IRelayCommand PlayCommand { get; }

        public TaskCompletionSource<LumoplayDevice> CompletionSource { get; } = new();

        public SelectDevicePopupViewModel(IEnumerable<LumoplayDevice> devices)
        {
            Devices = new ObservableCollection<LumoplayDevice>(devices);
            PlayCommand = new RelayCommand(OnPlay, CanPlay);
        }

        private bool CanPlay() => SelectedDevice != null;

        private void OnPlay()
        {
            CompletionSource.TrySetResult(SelectedDevice);
        }
    }
}