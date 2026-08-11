using System.Security.Claims;
using IntTech_Controller_Backend.Data;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Helpers;

/**
 * Rejects any authenticated request whose JWT SessionVersion claim no longer matches the
 * user's current SessionVersion in the database. This forces re-login after an admin edits
 * a user's role, locations, or tags.
 */
public class SessionVersionMiddleware(RequestDelegate next)
{
    /**
     * Validates the caller's session version and either passes the request on or
     * answers 401. Anonymous requests are passed through untouched; a token whose
     * id or version claim is missing or malformed is treated as expired.
     *
     * <param name="context">the request being processed</param>
     * <param name="db">database context used to read the user's current session version</param>
     */
    public async Task InvokeAsync(HttpContext context, IntTechDBContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessionVersionStr = context.User.FindFirstValue("SessionVersion");

            if (!ObjectId.TryParse(userIdStr, out var userId) ||
                !int.TryParse(sessionVersionStr, out var jwtVersion))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Session expired. Please log in again."
                });
                return;
            }

            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user != null && user.SessionVersion != jwtVersion)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Session expired. Please log in again."
                });
                return;
            }
        }

        await next(context);
    }
}
