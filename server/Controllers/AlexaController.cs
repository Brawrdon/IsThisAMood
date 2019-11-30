using System;
using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IsThisAMood.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AlexaController : ControllerBase
    {
        private readonly ILogger<AlexaController> _logger;
        private readonly IConfiguration _configuration;

        public AlexaController(ILogger<AlexaController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }
        
        [HttpPost]
        public ActionResult<SkillResponse> ReceiveRequest(SkillRequest skillRequest)
        {
            _logger.LogInformation("Alexa request received");
            _logger.LogDebug("Alexa skill ID: " + skillRequest.Context.System.Application.ApplicationId);

            if (!skillRequest.Context.System.Application.ApplicationId.Equals(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID")))
            {
                _logger.LogInformation("Alexa skill ID does not match");
                return new ObjectResult(null) {StatusCode = 403};
            }

            LogIntent(skillRequest.Request.Type);
            
            switch (skillRequest.Request.Type)
            {
                case "LaunchRequest":
                    return LaunchRequest();
                case "IntentRequest":
                    return IntentRequest(skillRequest);
                default:
                    return UnknownRequest();
            }
        }

        private SkillResponse LaunchRequest()
        {
            return BuildAskResponse(_configuration["Responses:LaunchRequest"]);
        }

        private SkillResponse IntentRequest(SkillRequest skillRequest)
        {
            var intentRequest = skillRequest.Request as IntentRequest;
            
            switch (intentRequest.Intent.Name)
            {
                case "HelloIntent":
                    return HelloIntent();
                default:
                    return UnknownRequest();
            }
        }

        private SkillResponse UnknownRequest()
        {
            return BuildAskResponse(_configuration["Responses:UnknownRequest"]);
        }

        private SkillResponse HelloIntent()
        {
            return BuildAskResponse(_configuration["Responses:HelloIntent"]);
        }

        private SkillResponse BuildAskResponse(string message, string repromptMessage = null)
        {
            if (repromptMessage == null)
                repromptMessage = message;
            
            var speech = new PlainTextOutputSpeech(message);
            var repromptSpeech = new PlainTextOutputSpeech(repromptMessage);
            
            var reprompt = new Reprompt { OutputSpeech = repromptSpeech };

            return ResponseBuilder.Ask(speech, reprompt);
        }

        private void LogIntent(string requestType, SkillRequest skillRequest = null)
        {
            if (skillRequest != null)
                LogSkillRequest(skillRequest);
            
            _logger.LogInformation("Intent reached: " + requestType);
        }

        private void LogSkillRequest(SkillRequest skillRequest)
        { 
            _logger.LogDebug("Skill request: " + skillRequest);
        }
    }
}