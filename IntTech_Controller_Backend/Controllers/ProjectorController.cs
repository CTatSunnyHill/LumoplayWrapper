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

        // GET: api/Projector
        [HttpGet]
        public async Task<IActionResult> GetAllProjectors()
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var locationsClaim = User.FindFirstValue("AllowedLocationsIds");
            var allowedLocationIdsStr = string.IsNullOrEmpty(locationsClaim) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(locationsClaim) ?? new List<string>();

            var allowedLocationIds = allowedLocationIdsStr.Where(idStr => ObjectId.TryParse(idStr,out _)).Select(ObjectId.Parse).ToList();

            var query = _dbContext.Projectors.AsQueryable();

            if (userRole.ToLower() != "admin")
            {
                query = query.Where(p => allowedLocationIds.Contains(p.LocationId));
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

        // GET: api/Projector/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProjectorById(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
            {
                return BadRequest("Invalid projector ID format.");
            }
            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();

            // Refresh Status 
            projector.Status = await _projectorService.GetPowerStatus(projector.IpAddress, projector.Port);
            projector.LastPolled = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return Ok(projector);

        }

        // POST: api/Projector
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

        // DELETE: api/Projector/{id}
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

        // POST: api/Projector/{id}/on
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

        // POST: api/Projector/{id}/off
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

        // POST: api/Projector/location/{locationId}/on
        [HttpPost("location/{locationId}/on")]
        public async Task<IActionResult> TurnLocationOn(string locationId) 
        {
            if (!ObjectId.TryParse(locationId, out ObjectId oid)) return BadRequest("Invalid Location ID");

            var projectors = await _dbContext.Projectors.Where(p => p.LocationId == oid).ToListAsync();

            if (!projectors.Any()) return NotFound($"No projectors found for LocationId {locationId}");

            var networkTasks = projectors.Select(async p =>
            {
                bool success = await _projectorService.SetPowerState(p.IpAddress, p.Port, true);
                return new { Projector = p, Success = success };
            });

           
            var results = await Task.WhenAll(networkTasks);

            foreach (var result in results)
            {
                result.Projector.Status = result.Success ? "warming" : "error";
            }
           
            await _dbContext.SaveChangesAsync();
            return Ok("Projectors turned on successfully.");
        }

        // POST: api/Projector/location/{locationId}/off
        [HttpPost("location/{locationId}/off")]
        public async Task<IActionResult> TurnLocationOff(string locationId)
        {
            if (!ObjectId.TryParse(locationId, out ObjectId oid)) return BadRequest("Invalid Location ID");

            var projectors = await _dbContext.Projectors.Where(p => p.LocationId == oid).ToListAsync();
            if (!projectors.Any()) return NotFound();

            var networkTasks = projectors.Select(async p =>
            {
                bool success = await _projectorService.SetPowerState(p.IpAddress, p.Port, false);
                return new { Projector = p, Success = success };
            });

         
            var results = await Task.WhenAll(networkTasks);
            foreach (var result in results)
            {
                result.Projector.Status = result.Success ? "cooling" : "error";
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { Message = "Power OFF commands processed." });
        }




    }
}
