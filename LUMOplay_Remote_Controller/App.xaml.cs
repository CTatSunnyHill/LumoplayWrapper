using LUMOplay_Remote_Controller.Services;

namespace LUMOplay_Remote_Controller
{
    public partial class App : Application
    {
        private readonly DeviceManager _deviceManager;

        public App(DeviceManager deviceManager)
        {
            InitializeComponent();
            _deviceManager = deviceManager;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override async void OnStart()
        {
            // This is the ideal place to perform the initial device handshake.
            await _deviceManager.InitializeDeviceConnectionsAsync();
        }
    }
}