
using LUMOplay_Remote_Controller.Model;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;


namespace LUMOplay_Remote_Controller.Services
{
    /**
     * Talks to the controller backend over HTTP. Registered as a singleton so
     * its HttpClient — and the connections it pools — are shared app-wide.
     */
    public class LumoPlayApiClient
    {
        private readonly HttpClient _httpClient;

        /** Creates the client against the backend named by <see cref="Constants.ApiUrl"/>. */
        public LumoPlayApiClient()
        {
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(Constants.ApiUrl)
            };
        }

            // --- Games ---
        /**
         * Fetches the full game catalogue from the backend. Failures are logged
         * and turned into an empty list, so a UI binding to the result never has
         * to handle an exception or a null.
         *
         * <returns>the catalogue, or an empty list when the backend could not be reached</returns>
         */
        public async Task<List<LumoplayGame>> GetAllGamesAsync()
        {
            try {
                Debug.WriteLine($"Trying to fetch games");
                return await _httpClient.GetFromJsonAsync<List<LumoplayGame>>("LumoRemote/lumoGames/get-all-games")
                    ?? new List<LumoplayGame>();
            } catch (Exception ex) {
                Console.WriteLine($"Error fetching games: {ex.Message}");
                return new List<LumoplayGame>();

            }
        }
    }
}

