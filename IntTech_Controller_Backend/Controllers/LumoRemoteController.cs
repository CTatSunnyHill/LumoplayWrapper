using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LumoRemoteController: ControllerBase
    {
        private readonly IntTechDBContext _context;
        private readonly LumoCommandService _commandService;

        public LumoRemoteController(IntTechDBContext context, LumoCommandService commandService) 
        {
            _context = context;
            _commandService = commandService;

        }

        // GET: api/LumoRemote/devices
        [HttpGet("devices")]
        public async Task<IActionResult> GetDevices()
        {
            var devices = await _context.Devices.ToListAsync();
            var threshold = DateTime.UtcNow.AddMinutes(-5);
            var needsSave = false;

            foreach (var device in devices)
            {
                if (device.isConnected && device.lastSeen < threshold)
                {
                    device.isConnected = false;
                    needsSave = true;
                }
            }

            if (needsSave)
            {
                await _context.SaveChangesAsync();
            }
            return Ok(devices);
        }


    }
}
