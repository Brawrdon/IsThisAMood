using System;
using System.Collections.Generic;
using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using IsThisAMood.Models;
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
        private readonly IDictionary<string, EntryActivities> _entryActivityStore;

        public AlexaController(ILogger<AlexaController> logger, IConfiguration configuration, IParticipantsService participantsService, IDictionary<string, EntryActivities> entryActivityStore)
        {
            _logger = logger;
            _configuration = configuration;
            _participantsService = participantsService;
            _entryActivityStore = entryActivityStore;
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
                case "BeginCreateEntry":
                    return BeginCreateEntry(skillRequest.Session.SessionId, intentRequest);
                case "AddActivity":
                    return AddActivity(skillRequest.Session.SessionId, intentRequest);
                default:
                    return UnknownRequest();
            }
        }



        private IActionResult UnknownRequest()
        {
            return BuildAskResponse(_configuration["Responses:UnknownRequest"]);
        }

        private IActionResult BeginCreateEntry(string sessionId, IntentRequest createEntryRequest)
        {
            var slots = createEntryRequest.Intent.Slots;
            var entry = new Entry
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Mood = slots["mood"].Value, 
                Rating = int.Parse(slots["rating"].Value), 
            };
            
            _entryActivityStore.Add(sessionId, new EntryActivities(entry));
            
            // ToDo: Check that the session is kept open so that activities can be added.
            return Ok(ResponseBuilder.Tell(_configuration["Responses:ActivitiesRequired"]));
        }
        
        // ToDo: Implement a complete create entry method
        private IActionResult AddActivity(string sessionId, IntentRequest intentRequest)
        {
            // Check the session is currently active
            if (_entryActivityStore.TryGetValue(sessionId, out var entryActivity) == false)
                return UnknownRequest();
            
            entryActivity.Activities.Add(intentRequest.Intent.Slots["activity"].Value);

            return BuildAskResponse(_configuration["Responses:ActivitiesRequest"]);

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