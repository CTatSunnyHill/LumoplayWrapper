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
                // Refresh the live input selection alongside status so the frontend has it without a separate call.
                projector.CurrentInput = await _projectorService.GetCurrentInput(projector.IpAddress, projector.Port);
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
            projector.CurrentInput = await _projectorService.GetCurrentInput(projector.IpAddress, projector.Port);
            projector.LastPolled = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return Ok(projector);

        }

        // POST: api/Projector
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddProjector([FromBody] ProjectorUpsertDto dto)
        {
            if (!ObjectId.TryParse(dto.LocationId, out ObjectId locOid))
                return BadRequest("Invalid LocationId format.");

            var projector = new Projector
            {
                Id = ObjectId.GenerateNewId(),
                Name = dto.Name,
                IpAddress = dto.IpAddress,
                Port = dto.Port,
                Password = dto.Password,
                LocationId = locOid,
                Status = "unknown",
                LastPolled = DateTime.UtcNow,
                Inputs = null,          // discovered later, on demand
                CurrentInput = null,
            };

            _dbContext.Projectors.Add(projector);
            await _dbContext.SaveChangesAsync();
            return Ok(projector);
        }

        // PUT: api/Projector/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateProjector(string id, [FromBody] ProjectorUpsertDto dto)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest("Invalid projector ID format.");
            if (!ObjectId.TryParse(dto.LocationId, out ObjectId locOid))
                return BadRequest("Invalid LocationId format.");

            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();

            projector.Name = dto.Name;
            projector.IpAddress = dto.IpAddress;
            projector.Port = dto.Port;
            projector.Password = dto.Password;
            projector.LocationId = locOid;
            // Inputs / CurrentInput / Status / LastPolled are intentionally NOT touched here.

            await _dbContext.SaveChangesAsync();
            return Ok(projector);
        }

        // POST: api/Projector/{id}/discover-inputs
        // Queries the projector via PJLink INST and merges results into stored Inputs,
        // PRESERVING existing labels. Idempotent and re-runnable.
        [HttpPost("{id}/discover-inputs")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DiscoverInputs(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest("Invalid projector ID format.");

            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();

            var discovered = await _projectorService.QueryAvailableInputs(projector.IpAddress, projector.Port);
            if (discovered.Count == 0)
                return StatusCode(503, "No inputs discovered. The projector may be offline or powered off.");

            var existing = projector.Inputs ?? new List<ProjectorInput>();

            // Merge: keep labels for codes still present; add new codes unlabelled; drop codes no longer reported.
            var merged = discovered.Select(code =>
            {
                var prior = existing.FirstOrDefault(e => e.Code == code);
                return new ProjectorInput { Code = code, Label = prior?.Label };
            }).ToList();

            projector.Inputs = merged;
            await _dbContext.SaveChangesAsync();
            return Ok(projector);
        }

        // PUT: api/Projector/{id}/input-labels
        // Sets/clears admin labels on already-discovered inputs. Does NOT add codes.
        [HttpPut("{id}/input-labels")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetInputLabels(string id, [FromBody] List<ProjectorInputLabelDto> labels)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest("Invalid projector ID format.");

            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();
            if (projector.Inputs == null || projector.Inputs.Count == 0)
                return BadRequest("No inputs to label. Run discovery first.");

            foreach (var input in projector.Inputs)
            {
                var match = labels.FirstOrDefault(l => l.Code == input.Code);
                if (match != null)
                {
                    // Normalize empty/whitespace to null so the clinician filter treats it as unlabelled.
                    input.Label = string.IsNullOrWhiteSpace(match.Label) ? null : match.Label.Trim();
                }
            }

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

        // POST: api/Projector/{id}/input/{code}
        // Switches the active input. Admins may target any discovered code;
        // clinicians may target only labelled codes within their allowed locations.
        // The projector must be powered on.
        [HttpPost("{id}/input/{code}")]
        public async Task<IActionResult> SetProjectorInput(string id, string code)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest("Invalid projector ID format.");

            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();

            var userRole = (User.FindFirstValue(ClaimTypes.Role) ?? "").ToLower();
            bool isAdmin = userRole == "admin";

            // ── Location scope (clinicians only) ──────────────────────────────
            if (!isAdmin)
            {
                var locationsClaim = User.FindFirstValue("AllowedLocationsIds");
                var allowedStr = string.IsNullOrEmpty(locationsClaim)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(locationsClaim) ?? new List<string>();
                var allowed = allowedStr.Where(s => ObjectId.TryParse(s, out _)).Select(ObjectId.Parse).ToList();
                if (!allowed.Contains(projector.LocationId))
                    return Forbid();
            }

            var inputs = projector.Inputs ?? new List<ProjectorInput>();

            // ── Role-aware code validation ────────────────────────────────────
            // Admin: code must exist among discovered inputs.
            // Clinician: code must exist AND be labelled (non-empty label).
            var match = inputs.FirstOrDefault(i => i.Code == code);
            if (match == null)
                return BadRequest("Input code is not available on this projector. Run discovery first.");
            if (!isAdmin && string.IsNullOrWhiteSpace(match.Label))
                return Forbid(); // labelled-only for clinicians

            // ── On-gate: live power check ─────────────────────────────────────
            var status = await _projectorService.GetPowerStatus(projector.IpAddress, projector.Port);
            if (status != "on")
                return Conflict($"Projector must be on to change its input (current status: {status}).");

            // ── Switch ────────────────────────────────────────────────────────
            bool ok = await _projectorService.SetInput(projector.IpAddress, projector.Port, code);
            if (!ok)
                return StatusCode(500, "Failed to switch projector input.");

            // Persist the new selection and refresh status timestamp.
            projector.CurrentInput = code;
            projector.Status = status;
            projector.LastPolled = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Ok(projector);
        }
    }
}
