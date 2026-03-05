using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using System.Security.Claims;
using System.Text.Json;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProjectorController : ControllerBase
    {
        private readonly IntTechDBContext _dbContext;
        private readonly ProjectorCommandService _projectorService;

        public ProjectorController(IntTechDBContext dbContext, ProjectorCommandService projectorService)
        {
            _dbContext = dbContext;
            _projectorService = projectorService;
        }

        // GET: api/projectors
        [HttpGet]
        public async Task<IActionResult> GetAllProjectors()
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var locationsClaim = User.FindFirstValue("AllowedLocations");
            var allowedLocations = string.IsNullOrEmpty(locationsClaim) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(locationsClaim) ?? new List<string>();

            var query = _dbContext.Projectors.AsQueryable();

            if (userRole.ToLower() != "admin")
            {
                query = query.Where(p => allowedLocations.Contains(p.Location));
            }

            var projectors = await query.ToListAsync();


            var tasks = projectors.Select(async projector =>
            {
                projector.Status = await _projectorService.GetPowerStatus(projector.IpAddress, projector.Port);
                projector.LastPolled = DateTime.UtcNow;
            });

            await Task.WhenAll(tasks);
            await _dbContext.SaveChangesAsync();

            return Ok(projectors);
        }

        // GET: api/projectors/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProjectorById(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
            {
                return BadRequest("Inavlid projector ID format.");
            }
            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();

            // Refresh Status 
            projector.Status = await _projectorService.GetPowerStatus(projector.IpAddress, projector.Port);
            projector.LastPolled = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return Ok(projector);

        }

        // POST: api/projectors
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddProjector([FromBody] Projector projector)
        {
            projector.Id = ObjectId.GenerateNewId();
            projector.Status = "Unknown";
            projector.LastPolled = DateTime.UtcNow;

            _dbContext.Projectors.Add(projector);
            await _dbContext.SaveChangesAsync();
            return Ok(projector);

        }

        // DELETE: api/projectors/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> DeleteProjector(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
            {
                return BadRequest("Inavlid projector ID format.");
            }
            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();
            _dbContext.Projectors.Remove(projector);
            await _dbContext.SaveChangesAsync();
            return Ok("Deleted projector successfully.");
        }

        // POST: api/projectors/{id}/on
        [HttpPost("{id}/on")]
        public async Task<IActionResult> TurnOn(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
            {
                return BadRequest("Inavlid projector ID format.");
            }
            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();

            bool success = await _projectorService.SetPowerState(projector.IpAddress, projector.Port, true);

            if (success)
            {
                projector.Status = "On";
                await _dbContext.SaveChangesAsync();
                return Ok("Projector turned on successfully.");
            }

            return StatusCode(500, "Failed to turn on the projector.");
        }

        // POST: api/projectors/{id}/off
        [HttpPost("{id}/off")]
        public async Task<IActionResult> TurnOff(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
            {
                return BadRequest("Inavlid projector ID format.");
            }
            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();
            bool success = await _projectorService.SetPowerState(projector.IpAddress, projector.Port, false);
            if (success)
            {
                projector.Status = "Off";
                await _dbContext.SaveChangesAsync();
                return Ok("Projector turned off successfully.");
            }
            return StatusCode(500, "Failed to turn off the projector.");
        }

        // POST: api/projectors/location/{location}/on
        [HttpPost("location/{location}/on")]
        public async Task<IActionResult> TurnLocationOn(string location) 
        {
            var projectors = await _dbContext.Projectors.Where(p => p.Location == location).ToListAsync();

            if (!projectors.Any()) return NotFound($"No projectors found in {location}");

            // Execute PJLink commands in parallel
            var tasks = projectors.Select(async p =>
            {
                bool success = await _projectorService.SetPowerState(p.IpAddress, p.Port, true);
                if (success)
                {
                    p.Status = "warming";
                }
                else 
                {
                    p.Status = "error";
                }

            });

            await Task.WhenAll(tasks);
            await _dbContext.SaveChangesAsync();

            return Ok("Projectors turned on successfully.");
        }

        // POST: api/projectors/location/{location}/off
        [HttpPost("location/{location}/off")]
        public async Task<IActionResult> TurnLocationOff(string location)
        {
            var projectors = await _dbContext.Projectors.Where(p => p.Location == location).ToListAsync();

            if (!projectors.Any()) return NotFound($"No projectors found in {location}");

            var tasks = projectors.Select(async p =>
            {
                bool success = await _projectorService.SetPowerState(p.IpAddress, p.Port, false);
                if (success)
                {
                    p.Status = "cooling";

                }
                else
                {
                    p.Status = "error";
                }
            });

            await Task.WhenAll(tasks);
            await _dbContext.SaveChangesAsync();

            return Ok("Power off commands processed for each projector properly.");


        }

        


    }
}
