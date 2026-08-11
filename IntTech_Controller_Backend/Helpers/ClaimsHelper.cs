using System.Security.Claims;
using System.Text.Json;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Helpers;

/**
 * Reads this system's claims off a signed-in principal. Every accessor is
 * total: a missing, malformed, or unparseable claim yields an empty value
 * rather than throwing, so a bad token narrows access instead of failing the
 * request.
 */
public static class ClaimsHelper
{
    /**
     * Reads the caller's role.
     *
     * <param name="user">the signed-in principal</param>
     * <returns>the role name, or an empty string when the claim is absent</returns>
     */
    public static string GetUserRole(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) ?? "";

    /**
     * Reads the caller's user id.
     *
     * <param name="user">the signed-in principal</param>
     * <returns>the user's ObjectId, or ObjectId.Empty when absent or malformed</returns>
     */
    public static ObjectId GetUserId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        return ObjectId.TryParse(idStr, out var oid) ? oid : ObjectId.Empty;
    }

    /**
     * Reads the locations the caller may control, stored as a JSON array of id strings.
     *
     * <param name="user">the signed-in principal</param>
     * <returns>the parseable location ids; empty when the claim is absent or invalid JSON</returns>
     */
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

    /**
     * Reads the tags gating which games the caller may see, stored as a JSON
     * array of id strings.
     *
     * <param name="user">the signed-in principal</param>
     * <returns>the parseable tag ids; empty when the claim is absent or invalid JSON</returns>
     */
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
