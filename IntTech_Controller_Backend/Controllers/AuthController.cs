using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace IntTech_Controller_Backend.Controllers
{
    /**
     * Issues the JWTs the rest of the API authenticates with. This is the only
     * controller reachable without a token.
     */
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController: ControllerBase
    {
        private readonly IntTechDBContext _context;
        private readonly IConfiguration _config;

        /**
         * <param name="context">database context used to look up the account</param>
         * <param name="config">configuration supplying the JWT signing key</param>
         */
        public AuthController(IntTechDBContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        /**
         * Signs a user in and returns a token valid for seven days. The token
         * carries the user's role, allowed locations, and allowed tags, so most
         * access checks need no database lookup; it also carries the session
         * version, which lets an admin's edits invalidate it immediately.
         *
         * <param name="request">the username and password to verify</param>
         * <returns>200 with the token and the user's identity; 400 when either
         * field is missing; 401 when the credentials do not match</returns>
         */
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Invalid request body" });
            }

            var username = request.Username?.Trim();
            var password = request.Password?.Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new { message = "Username and password are required" });
            }

            var normalizedUsername = username.ToLower();

            // One combined message for both failure modes, so the response does
            // not reveal whether the username exists.
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            // Id lists travel as JSON strings because a claim holds only one value.
            var locationIds = (user.AllowedLocationsIds ?? []).Select(id => id.ToString()).ToList();
            var tagIds = (user.AllowedTagIds ?? []).Select(id => id.ToString()).ToList();
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("SessionVersion", user.SessionVersion.ToString()),
                new Claim("AllowedLocationsIds", JsonSerializer.Serialize(locationIds)),
                new Claim("AllowedTagIds", JsonSerializer.Serialize(tagIds))
            };

            var jwtKey = _config["Jwt:Key"] ?? "SuperSecretKeyForIntTechHospitalAppThatIsLongEnough";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
                );

            return Ok(new
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Role = user.Role,
                Username = user.Username,
                UserId = user.Id.ToString(),
            });

        }
    }
}
