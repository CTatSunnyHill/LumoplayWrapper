using System.Security.Claims;
using System.Text.Json;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Helpers;

public static class ClaimsHelper
{
    public static string GetUserRole(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) ?? "";

    public static List<ObjectId> GetAllowedLocationIds(ClaimsPrincipal user)
    {
        var locationsClaim = user.FindFirstValue("AllowedLocationsIds");
        var allowedLocationIdsStr = string.IsNullOrEmpty(locationsClaim)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(locationsClaim) ?? new List<string>();

        return allowedLocationIdsStr
            .Where(idStr => ObjectId.TryParse(idStr, out _))
            .Select(ObjectId.Parse)
            .ToList();
    }
}
