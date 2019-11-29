using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IsThisAMood.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AlexaController : ControllerBase
    {
        private readonly ILogger<AlexaController> _logger;

        public AlexaController(ILogger<AlexaController> logger)
        {
            _logger = logger;
        }
        
        [HttpPost]
        public ActionResult<SkillResponse> ReceiveRequest(SkillRequest skillRequest)
        {
            var finalResponse = ResponseBuilder.Tell(new PlainTextOutputSpeech("I got your request."));
            return finalResponse;
        }
    }
}