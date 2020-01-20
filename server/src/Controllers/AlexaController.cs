using System;
using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using IsThisAMood.Models.Database;
using IsThisAMood.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace IsThisAMood.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AlexaController : ControllerBase
    {
        private readonly ILogger<AlexaController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IParticipantsService _participantsService;

        public AlexaController(ILogger<AlexaController> logger, IConfiguration configuration, IParticipantsService participantsService)
        {
            _logger = logger;
            _configuration = configuration;
            _participantsService = participantsService;
        }
        
        [HttpPost]
        public IActionResult ReceiveRequest(SkillRequest skillRequest)
        {
            _logger.LogInformation("Alexa request received");
            _logger.LogDebug("Alexa skill ID: " + skillRequest.Context.System.Application.ApplicationId);

            if (!skillRequest.Context.System.Application.ApplicationId.Equals(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID")))
            {
                _logger.LogInformation("Alexa skill ID does not match");
                return Forbid();
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

        private IActionResult LaunchRequest()
        {
            return BuildAskResponse(_configuration["Responses:LaunchRequest"]);
        }

        private IActionResult IntentRequest(SkillRequest skillRequest)
        {
            var intentRequest = skillRequest.Request as IntentRequest;
            
            switch (intentRequest.Intent.Name)
            {
                case "CreateEntry":
                    return CreateEntry(intentRequest);
                default:
                    return UnknownRequest();
            }
        }

        private IActionResult UnknownRequest()
        {
            return BuildAskResponse(_configuration["Responses:UnknownRequest"]);
        }

        private IActionResult CreateEntry(IntentRequest createEntryRequest)
        {
            var slots = createEntryRequest.Intent.Slots;
            var entry = new Entry
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Mood = slots["mood"].Value, 
                Rating = int.Parse(slots["rating"].Value), 
            };
            
            return Ok(ResponseBuilder.Tell(_configuration["Responses:ActivitiesRequired"]));
        }

        private IActionResult BuildAskResponse(string message, string repromptMessage = null)
        {
            if (repromptMessage == null)
                repromptMessage = message;
            
            var speech = new PlainTextOutputSpeech(message);
            var repromptSpeech = new PlainTextOutputSpeech(repromptMessage);
            
            var reprompt = new Reprompt { OutputSpeech = repromptSpeech };

            return Ok(ResponseBuilder.Ask(speech, reprompt));
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