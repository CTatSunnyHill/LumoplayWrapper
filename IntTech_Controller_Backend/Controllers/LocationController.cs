using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocationController: ControllerBase
    {
        private readonly IntTechDBContext _context;

        public LocationController(IntTechDBContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _context.Locations.ToListAsync();
            return Ok(locations);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateLocation([FromBody] LocationDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { Message = "Name is required" });
            var exists = await _context.Locations.AnyAsync(l => l.Name.ToLower() == dto.Name.ToLower());
            if (exists) return BadRequest(new { Message = "A location with this name already exists" });

            var newLocation = new Location
            {
                Id = ObjectId.GenerateNewId(),
                Name = dto.Name.Trim(),
            };

            _context.Locations.Add(newLocation);
            await _context.SaveChangesAsync();
            return Ok(newLocation);
        }

        [HttpPut]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateLocation(string id, [FromBody] LocationDto dto)
        {
            if (ObjectId.TryParse(id, out ObjectId oid)) return BadRequest("Invalid ID format");

            var location  = await _context.Locations.FirstOrDefaultAsync(l => l.Id == oid);
            if (location == null) return NotFound(new { Message = "Location not found"});

            var exists = await _context.Locations.AnyAsync(l => l.Name.ToLower() == dto.Name.ToLower() && l.Id != oid);
            if (exists) return BadRequest(new { Message = "A location with this name already exists" });

            location.Name = dto.Name.Trim();
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Location renamed successfully" });
        }


        [HttpDelete]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteLocation(string id)
        {
            if (ObjectId.TryParse(id, out ObjectId oid)) return BadRequest("Invalid ID format");

            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == oid);
            if (location == null) return NotFound(new { Message = "Location not found" });

            bool inUseByDevice = await _context.Devices.AnyAsync(d => d.LocationId == oid);
            bool inUseByProjector = await _context.Projectors.AnyAsync(p => p.LocationId == oid);

            if (inUseByDevice || inUseByProjector)
            {
                return BadRequest(new { Message = "Cannot delete this location because it is currently assigned to a device or a projector" });
            }

            _context.Locations.Remove(location);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Location Deleted" });

        }
    }

    public class LocationDto
    {
        public string Name { get; set; }
    }
}
