using System.Security.Claims;
using System.Text.Json;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Helpers;

public static class ClaimsHelper
{
    public static string GetUserRole(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) ?? "";

    public static ObjectId GetUserId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        return ObjectId.TryParse(idStr, out var oid) ? oid : ObjectId.Empty;
    }

    public static List<ObjectId> GetAllowedLocationIds(ClaimsPrincipal user)
    {
        var locationsClaim = user.FindFirstValue("AllowedLocationsIds");
        List<string> allowedLocationIdsStr;

        if (string.IsNullOrEmpty(locationsClaim))
        {
            allowedLocationIdsStr = new List<string>();
        }
        else
        {
            try
            {
                allowedLocationIdsStr = JsonSerializer.Deserialize<List<string>>(locationsClaim) ?? new List<string>();
            }
            catch (JsonException)
            {
                allowedLocationIdsStr = new List<string>();
            }
        }

        return allowedLocationIdsStr
            .Where(idStr => ObjectId.TryParse(idStr, out _))
            .Select(ObjectId.Parse)
            .ToList();
    }

    public static List<ObjectId> GetAllowedTagIds(ClaimsPrincipal user)
    {
        var tagsClaim = user.FindFirstValue("AllowedTagIds");
        List<string> allowedTagIdsStr;

        if (string.IsNullOrEmpty(tagsClaim))
        {
            allowedTagIdsStr = new List<string>();
        }
        else
        {
            try
            {
                allowedTagIdsStr = JsonSerializer.Deserialize<List<string>>(tagsClaim) ?? new List<string>();
            }
            catch (JsonException)
            {
                allowedTagIdsStr = new List<string>();
            }
        }

        return allowedTagIdsStr
            .Where(idStr => ObjectId.TryParse(idStr, out _))
            .Select(ObjectId.Parse)
            .ToList();
    }
}
