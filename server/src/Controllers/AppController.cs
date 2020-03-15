using IsThisAMood.Models.Requests;
using IsThisAMood.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IsThisAMood.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AppController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AppController> _logger;
        private readonly IParticipantsAuthenticationService _participantsAuthenticationService;
        private readonly IParticipantsService _participantsService;


        public AppController(ILogger<AppController> logger, IConfiguration configuration,
            IParticipantsService participantsService,
            IParticipantsAuthenticationService participantsAuthenticationService)
        {
            _logger = logger;
            _configuration = configuration;
            _participantsService = participantsService;
            _participantsAuthenticationService = participantsAuthenticationService;
        }

        [HttpGet]
        [Route("entries")]
        public IActionResult GetEntries([FromQuery] string mood) 
        {
            var accessToken = Request.Headers["Authorization"];
            var pin = Request.Headers["Pin"];

            _logger.LogDebug(accessToken);
            var entries = _participantsService.GetEntries(_participantsAuthenticationService.GetHashedString(accessToken), pin, mood);

            return Ok(entries);

        }

    }

}