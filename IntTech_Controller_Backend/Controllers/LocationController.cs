using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers
{
    /**
     * Manages the rooms and sites that devices and projectors are assigned to.
     * Any signed-in user may read locations; only admins may change them.
     */
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocationController: ControllerBase
    {
        private readonly IntTechDBContext _context;

        /**
         * <param name="context">database context for the locations collection</param>
         */
        public LocationController(IntTechDBContext context)
        {
            _context = context;
        }

        /**
         * Lists every location.
         *
         * <returns>200 with all locations, unfiltered by the caller's own access</returns>
         */
        [HttpGet]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _context.Locations.ToListAsync();
            return Ok(locations);
        }

        /**
         * Creates a location. Names are unique, compared case-insensitively.
         *
         * <param name="dto">the name to give the new location</param>
         * <returns>200 with the created location; 400 when the name is blank or taken</returns>
         */
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

        /**
         * Renames a location.
         *
         * <param name="id">string form of the location's ObjectId</param>
         * <param name="dto">the new name</param>
         * <returns>200 on success; 400 for a malformed id or a name already in
         * use by another location; 404 when no such location exists</returns>
         */
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateLocation(string id, [FromBody] LocationDto dto)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid)) return BadRequest("Invalid ID format");

            var location  = await _context.Locations.FirstOrDefaultAsync(l => l.Id == oid);
            if (location == null) return NotFound(new { Message = "Location not found"});

            var exists = await _context.Locations.AnyAsync(l => l.Name.ToLower() == dto.Name.ToLower() && l.Id != oid);
            if (exists) return BadRequest(new { Message = "A location with this name already exists" });

            location.Name = dto.Name.Trim();
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Location renamed successfully" });
        }


        /**
         * Deletes a location, refusing while any device or projector still
         * points at it so no equipment is orphaned.
         *
         * <param name="id">string form of the location's ObjectId</param>
         * <returns>200 on success; 400 for a malformed id or a location still in
         * use; 404 when no such location exists</returns>
         */
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteLocation(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid)) return BadRequest("Invalid ID format");

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

    /** Request body for creating or renaming a location. */
    public class LocationDto
    {
        /** The location name to set. */
        public string Name { get; set; }
    }
}
