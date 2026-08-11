using LUMOplay_Remote_Controller.Services;

namespace LUMOplay_Remote_Controller
{
    /**
     * Application root. Creates the shell window and kicks off the first device
     * poll once the app is running.
     */
    public partial class App : Application
    {
        private readonly DeviceManager _deviceManager;

        /**
         * <param name="deviceManager">service that tracks device connectivity, injected by DI</param>
         */
        public App(DeviceManager deviceManager)
        {
            InitializeComponent();
            _deviceManager = deviceManager;
        }

        /**
         * Builds the app's single window around the navigation shell.
         *
         * <param name="activationState">platform activation details; unused</param>
         * <returns>the window to display</returns>
         */
        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        /**
         * Contacts every configured device once the app has started, so the
         * dashboard shows live status rather than waiting for the first user
         * action. Fire-and-forget: startup is not blocked on unreachable
         * devices.
         */
        protected override async void OnStart()
        {
            // This is the ideal place to perform the initial device handshake.
            await _deviceManager.InitializeDeviceConnectionsAsync();
        }
    }
}
