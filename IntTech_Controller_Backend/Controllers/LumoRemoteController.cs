using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LumoRemoteController : ControllerBase
    {
        private readonly IntTechDBContext _context;
        private readonly LumoCommandService _commandService;

        public LumoRemoteController(IntTechDBContext context, LumoCommandService commandService)
        {
            _context = context;
            _commandService = commandService;
        }
    }
}
