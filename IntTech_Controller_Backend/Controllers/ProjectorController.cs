using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
            var projectors = await _dbContext.Projectors.ToListAsync();

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
    }
}
