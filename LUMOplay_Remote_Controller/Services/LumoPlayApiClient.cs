
using LUMOplay_Remote_Controller.Model;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;


namespace LUMOplay_Remote_Controller.Services
{
    public class LumoPlayApiClient
    {
        private readonly HttpClient _httpClient;

        public LumoPlayApiClient()
        {
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(Constants.ApiUrl)
            };
        }

            // --- Games ---
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

