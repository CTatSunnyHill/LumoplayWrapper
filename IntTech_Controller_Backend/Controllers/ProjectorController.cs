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
    /**
     * Projector inventory and control over PJLink. Read endpoints poll the
     * hardware and persist what they find. Inventory changes are admin-only;
     * power and input control are open to any signed-in user, subject to the
     * per-endpoint scope checks noted below.
     */
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProjectorController : ControllerBase
    {
        private readonly IntTechDBContext _dbContext;
        private readonly ProjectorCommandService _projectorService;

        /**
         * <param name="dbContext">database context for the projectors collection</param>
         * <param name="projectorService">service used to talk PJLink to the hardware</param>
         */
        public ProjectorController(IntTechDBContext dbContext, ProjectorCommandService projectorService)
        {
            _dbContext = dbContext;
            _projectorService = projectorService;
        }

        /**
         * Lists the projectors the caller may control, polling each in parallel
         * for its power state and selected input before answering.
         *
         * <returns>200 with the visible projectors and their refreshed state</returns>
         */
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

        /**
         * Fetches one projector, polling it for fresh power state and input.
         *
         * <param name="id">string form of the projector's ObjectId</param>
         * <returns>200 with the refreshed projector; 400 for a malformed id;
         * 404 when not found</returns>
         */
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

        /**
         * Registers a projector. Its inputs are left undiscovered — run
         * <see cref="DiscoverInputs"/> once the unit is reachable.
         *
         * <param name="dto">the connection details and owning location</param>
         * <returns>200 with the stored projector; 400 for a malformed location id</returns>
         */
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

        /**
         * Updates a projector's connection details. Discovered inputs, labels,
         * and polled state are deliberately preserved, so editing a name or
         * port does not force a re-discovery.
         *
         * <param name="id">string form of the projector's ObjectId</param>
         * <param name="dto">the replacement connection details</param>
         * <returns>200 with the updated projector; 400 for a malformed id;
         * 404 when not found</returns>
         */
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

        /**
         * Queries the projector via PJLink INST and merges results into stored Inputs,
         * PRESERVING existing labels. Idempotent and re-runnable.
         *
         * <param name="id">string form of the projector's ObjectId</param>
         * <returns>200 with the updated projector; 400 for a malformed id;
         * 404 when not found; 503 when the projector reported nothing, which
         * usually means it is off or unreachable</returns>
         */
        // POST: api/Projector/{id}/discover-inputs
        [HttpPost("{id}/discover-inputs")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DiscoverInputs(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest("Invalid projector ID format.");

            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();

            // Treat an empty result as a failure, not as "this projector has no
            // inputs" — otherwise polling an off projector would wipe the labels.
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

        /**
         * Sets/clears admin labels on already-discovered inputs. Does NOT add codes.
         * Labelling matters beyond cosmetics: clinicians may only switch to
         * inputs that carry a label.
         *
         * <param name="id">string form of the projector's ObjectId</param>
         * <param name="labels">the labels to apply, matched by input code;
         * codes not present on the projector are ignored</param>
         * <returns>200 with the updated projector; 400 for a malformed id or a
         * projector whose inputs have not been discovered; 404 when not found</returns>
         */
        // PUT: api/Projector/{id}/input-labels
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

        /**
         * Removes a projector from the inventory. The unit itself is untouched.
         *
         * <param name="id">string form of the projector's ObjectId</param>
         * <returns>200 on success; 400 for a malformed id; 404 when not found</returns>
         */
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

        /**
         * Powers one projector on.
         *
         * <param name="id">string form of the projector's ObjectId</param>
         * <returns>200 on success; 400 for a malformed id; 404 when not found;
         * 500 when the projector did not accept the command</returns>
         */
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

        /**
         * Powers one projector off.
         *
         * <param name="id">string form of the projector's ObjectId</param>
         * <returns>200 on success; 400 for a malformed id; 404 when not found;
         * 500 when the projector did not accept the command</returns>
         */
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

        /**
         * Powers on every projector in a room at once. Commands are sent in
         * parallel and each projector's status records its own result, so one
         * unreachable unit does not fail the request.
         *
         * NOTE: unlike the per-projector endpoints, this does not check the
         * caller's allowed locations.
         *
         * <param name="locationId">string form of the location's ObjectId</param>
         * <returns>200 once every command has been attempted; 400 for a
         * malformed id; 404 when the location has no projectors</returns>
         */
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

            // "warming" rather than "on": the lamp takes time, and the next poll
            // will report the settled state.
            foreach (var result in results)
            {
                result.Projector.Status = result.Success ? "warming" : "error";
            }

            await _dbContext.SaveChangesAsync();
            return Ok("Projectors turned on successfully.");
        }

        /**
         * Powers off every projector in a room at once, as the mirror of
         * <see cref="TurnLocationOn"/> and with the same lack of a location
         * scope check.
         *
         * <param name="locationId">string form of the location's ObjectId</param>
         * <returns>200 once every command has been attempted; 400 for a
         * malformed id; 404 when the location has no projectors</returns>
         */
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
            // "cooling" rather than "off": the lamp takes time to shut down.
            foreach (var result in results)
            {
                result.Projector.Status = result.Success ? "cooling" : "error";
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { Message = "Power OFF commands processed." });
        }

        /**
         * Switches the active input. Admins may target any discovered code;
         * clinicians may target only labelled codes within their allowed locations.
         * The projector must be powered on.
         *
         * <param name="id">string form of the projector's ObjectId</param>
         * <param name="code">PJLink input code to switch to</param>
         * <returns>200 with the updated projector; 400 for a malformed id or an
         * undiscovered code; 403 when a clinician targets another location or an
         * unlabelled input; 404 when not found; 409 when the projector is not on;
         * 500 when the switch was refused</returns>
         */
        // POST: api/Projector/{id}/input/{code}
        [HttpPost("{id}/input/{code}")]
        public async Task<IActionResult> SetProjectorInput(string id, string code)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest("Invalid projector ID format.");

            var projector = await _dbContext.Projectors.FirstOrDefaultAsync(p => p.Id == oid);
            if (projector == null) return NotFound();

            bool isAdmin = User.IsInRole("Admin");

            // ── Location scope (clinicians only) ──────────────────────────────
            if (!isAdmin)
            {
                var allowed = IntTech_Controller_Backend.Helpers.ClaimsHelper.GetAllowedLocationIds(User);
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
