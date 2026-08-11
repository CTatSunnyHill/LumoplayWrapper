namespace LUMOplay_Remote_Controller
{
    /** App-wide constants. */
    public class Constants
    {
        /**
         * Base address of the controller backend, with trailing slash so it can
         * be combined with relative endpoint paths. Points at localhost, so it
         * must be changed to the server's address before deploying to a tablet.
         */
        public const string ApiUrl = "http://localhost:5221/api/";
    }
}
