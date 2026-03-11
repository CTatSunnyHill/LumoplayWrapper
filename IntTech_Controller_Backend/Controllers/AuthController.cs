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
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController: ControllerBase
    {
        private readonly IntTechDBContext _context;
        private readonly IConfiguration _config;

        public AuthController(IntTechDBContext context, IConfiguration config) 
        {
            _context = context;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request) 
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password,user.PasswordHash)) 
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }
            var claims = new List<Claim> 
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("AllowedLocations", JsonSerializer.Serialize(user.AllowedLocations))
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
            });

        }
    }
}
