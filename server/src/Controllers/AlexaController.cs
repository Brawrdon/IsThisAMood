using System;
using System.Collections.Generic;
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
        private readonly CreateEntryStore _createEntryStore;
        private readonly SessionStore _sessionStore;

        public AlexaController(ILogger<AlexaController> logger, IConfiguration configuration, IParticipantsService participantsService, CreateEntryStore createEntryStore)
        {
            _logger = logger;
            _configuration = configuration;
            _participantsService = participantsService;
            _createEntryStore = createEntryStore;
        }
        
        [HttpPost]
        public IActionResult ReceiveRequest(SkillRequest skillRequest)
        {
            string skillID = skillRequest.Context.System.Application.ApplicationId;
            if (!skillID.Equals(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID")))
            {
                _logger.LogWarning("Incorrect skill ID : {SkillID}", skillID);
                return Unauthorized();
            }
            
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
            _logger.LogInformation("Launch request recieved.");
            return Ok(BuildAskResponse(_configuration["Responses:LaunchRequest"]));
        }

        
        private IActionResult IntentRequest(SkillRequest skillRequest)
        {
            var intentRequest = skillRequest.Request as IntentRequest;

            _logger.LogDebug("{Intent}", intentRequest.Intent.Name);

            switch (intentRequest.Intent.Name)
            {
                case "CreateEntry":
                    return CreateEntry(skillRequest.Session.SessionId, intentRequest);
                case "AddActivity":
                    return AddActivity(skillRequest.Session, intentRequest);
                case "AMAZON.YesIntent":
                    return YesIntent(skillRequest.Session);
                case "AMAZON.NoIntent":
                    return NoIntent(skillRequest.Session);
                default:
                    return UnknownRequest();
            }
        }

        private IActionResult YesIntent(Alexa.NET.Request.Session session)
        {
            // Check that a session is currently in the createEntryStore
            if (_createEntryStore.Entries.TryGetValue(session.SessionId, out var entry))
            {
                return Ok(ResponseBuilder.DialogDelegate(session, new Intent { Name = "AddActivity"}));
            }
            
            return UnknownRequest();
        }

        private IActionResult NoIntent(Alexa.NET.Request.Session session)
        {
            // Check that a session is currently in the createEntryStore
            if (_createEntryStore.Entries.TryGetValue(session.SessionId, out var entry))
            {   
                // ToDo: Create proper participant IDs
                _participantsService.AddEntry("5ded84556acef0f6eff6da6f", entry);
                return Ok(ResponseBuilder.Tell(_configuration["Responses:EntryAdded"]));
            }
            
            return UnknownRequest();
        }
        private IActionResult UnknownRequest()
        {
            return Ok(BuildAskResponse(_configuration["Responses:UnknownRequest"]));
        }

        private IActionResult CreateEntry(string sessionId, IntentRequest createEntryRequest)
        {
            var slots = createEntryRequest.Intent.Slots;
            var entry = new Entry
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Mood = slots["mood"].Value, 
                Rating = int.Parse(slots["rating"].Value),
                Activities = new List<string>()
            };
            
            _createEntryStore.Entries.Add(sessionId, entry);
            
            return Ok(BuildAskResponse(_configuration["Responses:FirstActivityRequest"]));
        }

        private IActionResult AddActivity(Alexa.NET.Request.Session session, IntentRequest intentRequest)
        {
            // Check the session is currently active
            if (_createEntryStore.Entries.TryGetValue(session.SessionId, out var entry) == false)
                return UnknownRequest();
            
            entry.Activities.Add(intentRequest.Intent.Slots["activity"].Value);

            return Ok(BuildAskResponse(_configuration["Responses:ActivityRequest"]));
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
    }
}