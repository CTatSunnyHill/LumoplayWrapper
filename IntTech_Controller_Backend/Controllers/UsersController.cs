using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers
{
    /**
     * Account administration. Admin-only in its entirety. The seeded "admin"
     * account is protected here: it cannot be deleted or demoted, so the system
     * can never be left without an administrator.
     */
    [ApiController]
    [Route("/api/[Controller]")]
    [Authorize(Roles = "Admin")]

    public class UsersController: ControllerBase
    {
        private readonly IntTechDBContext _context;

        /**
         * <param name="context">database context for the users and playlists collections</param>
         */
        public UsersController(IntTechDBContext context)
        {
            _context = context;
        }


        /**
         * Lists every account without its password hash.
         *
         * <returns>200 with all users, ids rendered as strings</returns>
         */
        // GET: api/Users
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            // 1. Fetch raw users from the database FIRST (Executes the DB query)
            // This prevents the EF Core MongoDB provider from crashing (500 error)
            var dbUsers = await _context.Users.ToListAsync();

            // 2. Map the data in-memory to safely hide the PasswordHash and format IDs
            var users = dbUsers.Select(u => new
            {
                Id = u.Id.ToString(),
                u.Username,
                u.Role,
                AllowedLocationsIds = (u.AllowedLocationsIds ?? []).Select(id => id.ToString()).ToList(),
                AllowedTagIds = (u.AllowedTagIds ?? []).Select(id => id.ToString()).ToList()
            });

            return Ok(users);
        }


        /**
         * Creates an account with a BCrypt-hashed password. Usernames are
         * unique, compared case-insensitively. Location and tag ids that do not
         * parse are silently skipped rather than failing the request.
         *
         * <param name="dto">the credentials, role, and access grants</param>
         * <returns>200 on success; 400 when the body is missing, a required
         * field is blank, or the username is taken</returns>
         */
        // POST: api/users
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { Message = "Request body is required" });
            }

            var username = dto.Username?.Trim();
            var password = dto.Password?.Trim();
            var role = dto.Role?.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { Message = "Username is required" });
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new { Message = "Password is required" });
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                return BadRequest(new { Message = "Role is required" });
            }

            var usernameLower = username.ToLowerInvariant();
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == usernameLower);
            if (existingUser != null)
            {
                return BadRequest(new { Message = "Username already exists" });
            }

            var locationIds = new List<ObjectId>();

            if (dto.AllowedLocationsIds != null)
            {
                foreach (var idStr in dto.AllowedLocationsIds)
                {
                    if (ObjectId.TryParse(idStr, out ObjectId oid))
                    {
                        locationIds.Add(oid);
                    }
                }
            }

            var tagIds = new List<ObjectId>();

            if (dto.AllowedTagIds != null)
            {
                foreach (var idStr in dto.AllowedTagIds)
                {
                    if (ObjectId.TryParse(idStr, out ObjectId oid))
                    {
                        tagIds.Add(oid);
                    }
                }
            }

            var newUser = new User
            {
                Id = ObjectId.GenerateNewId(),
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = role,
                SessionVersion = 0,
                AllowedLocationsIds = locationIds,
                AllowedTagIds = tagIds,
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "User created successfully"});
        }

        /**
         * Deletes an account along with every playlist it owns. Call
         * <see cref="GetPlaylistImpact"/> first to show the admin what will be
         * lost — including any shared defaults this user published.
         *
         * <param name="id">string form of the user's ObjectId</param>
         * <returns>200 on success; 400 for a malformed id or the master admin;
         * 404 when not found</returns>
         */
        // DELETE: api/users/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid)) return BadRequest("Invalid ID");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == oid);
            if (user == null) return NotFound();

            if (user.Username.ToLower() == "admin") return BadRequest("Cannot delete the master admin");

            // Cascade: delete all playlists owned by this user (personal + any defaults they published).
            var userPlaylists = await _context.Playlists.Where(p => p.OwnerId == oid).ToListAsync();
            _context.Playlists.RemoveRange(userPlaylists);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "User deleted" });
        }

        /**
         * Counts what deleting a user would take with them, so the UI can warn
         * before the cascade in <see cref="DeleteUser"/> runs.
         *
         * <param name="userId">string form of the user's ObjectId</param>
         * <returns>200 with personalCount and defaultCount; 400 for a malformed id</returns>
         */
        // GET: api/Users/{userId}/playlist-impact
        // Returns counts of personal and default playlists that would be deleted with the user.
        [HttpGet("{userId}/playlist-impact")]
        public async Task<IActionResult> GetPlaylistImpact(string userId)
        {
            if (!ObjectId.TryParse(userId, out ObjectId oid)) return BadRequest("Invalid ID");

            var personalCount = await _context.Playlists
                .CountAsync(p => p.OwnerId == oid && !p.IsDefault);
            var defaultCount = await _context.Playlists
                .CountAsync(p => p.OwnerId == oid && p.IsDefault);

            return Ok(new { personalCount, defaultCount });
        }

        /**
         * Changes a user's role or access grants. Any real change bumps their
         * session version, which invalidates tokens already issued to them and
         * forces a re-login on their next request. Unchanged values are detected
         * by set comparison so a no-op save does not log anyone out.
         *
         * <param name="id">string form of the user's ObjectId</param>
         * <param name="dto">the fields to change; nulls are left alone</param>
         * <returns>200 with the new session version; 400 for a malformed id, an
         * unknown role, or an attempt to demote the master admin; 404 when not found</returns>
         */
        // PUT: api/users/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDto dto)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest(new { Message = "Invalid ID" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == oid);
            if (user == null) return NotFound(new { Message = "User not found" });

            if (user.Username.ToLower() == "admin" && dto.Role != null && dto.Role != "Admin")
                return BadRequest(new { Message = "Cannot change role of the master admin" });

            var changed = false;

            if (dto.Role != null && dto.Role != user.Role)
            {
                if (dto.Role != "Admin" && dto.Role != "User")
                    return BadRequest(new { Message = "Invalid role" });
                user.Role = dto.Role;
                changed = true;
            }

            if (dto.AllowedLocationsIds != null)
            {
                var parsedAllowedLocationIds = dto.AllowedLocationsIds
                    .Where(s => ObjectId.TryParse(s, out _))
                    .Select(ObjectId.Parse)
                    .ToList();

                if (!(user.AllowedLocationsIds ?? []).ToHashSet().SetEquals(parsedAllowedLocationIds))
                {
                    user.AllowedLocationsIds = parsedAllowedLocationIds;
                    changed = true;
                }
            }

            if (dto.AllowedTagIds != null)
            {
                var parsedAllowedTagIds = dto.AllowedTagIds
                    .Where(s => ObjectId.TryParse(s, out _))
                    .Select(ObjectId.Parse)
                    .ToList();

                if (!(user.AllowedTagIds ?? []).ToHashSet().SetEquals(parsedAllowedTagIds))
                {
                    user.AllowedTagIds = parsedAllowedTagIds;
                    changed = true;
                }
            }

            if (changed)
            {
                user.SessionVersion++;
                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = "User updated", sessionVersion = user.SessionVersion });
        }
    }
}

/** Request body for creating an account. */
public class CreateUserDto
{
    /** Login name; must not already be taken. */
    public string Username { get; set; }
    /** Plain-text password, hashed before it is stored. */
    public string Password { get; set; }
    /** Access level to grant: "Admin" or "User". */
    public string Role { get; set; }
    /** String forms of the location ObjectIds this user may control. */
    public List<string> AllowedLocationsIds { get; set; } = new List<string>();
    /** String forms of the tag ObjectIds gating which games this user may see. */
    public List<string> AllowedTagIds { get; set; } = new List<string>();
}

/** Request body for editing an account. Null members are left unchanged. */
public class UpdateUserDto
{
    /** New role, or null to keep the current one. */
    public string? Role { get; set; }
    /** Replacement allowed locations, or null to keep the current ones. */
    public List<string>? AllowedLocationsIds { get; set; }
    /** Replacement allowed tags, or null to keep the current ones. */
    public List<string>? AllowedTagIds { get; set; }
}
