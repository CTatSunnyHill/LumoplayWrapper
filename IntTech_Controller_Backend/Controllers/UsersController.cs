using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("/api/[Controller]")]
    [Authorize(Roles = "Admin")]

    public class UsersController: ControllerBase
    {
        private readonly IntTechDBContext _context;

        public UsersController(IntTechDBContext context)
        {
            _context = context;
        }


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
                user.AllowedLocationsIds = dto.AllowedLocationsIds
                    .Where(s => ObjectId.TryParse(s, out _))
                    .Select(ObjectId.Parse)
                    .ToList();
                changed = true;
            }

            if (dto.AllowedTagIds != null)
            {
                user.AllowedTagIds = dto.AllowedTagIds
                    .Where(s => ObjectId.TryParse(s, out _))
                    .Select(ObjectId.Parse)
                    .ToList();
                changed = true;
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

public class CreateUserDto
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string Role { get; set; }
    public List<string> AllowedLocationsIds { get; set; } = new List<string>();
    public List<string> AllowedTagIds { get; set; } = new List<string>();
}

public class UpdateUserDto
{
    public string? Role { get; set; }
    public List<string>? AllowedLocationsIds { get; set; }
    public List<string>? AllowedTagIds { get; set; }
}
