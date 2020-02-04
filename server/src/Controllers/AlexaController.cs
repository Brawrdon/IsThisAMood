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
            
            _logger.LogDebug("Request type : {AlexaRequest}", skillRequest.Request.Type);

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
            var responseText = _configuration["Responses:LaunchRequest"];
            LogSkillResponse(responseText);
            return Ok(BuildAskResponse(responseText));
        }

        private IActionResult IntentRequest(SkillRequest skillRequest)
        {
            var intentRequest = skillRequest.Request as IntentRequest;

            _logger.LogDebug("Intent launched : {Intent}", intentRequest.Intent.Name);

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
                    _logger.LogError("{Intent} is not a registered intent", intentRequest.Intent.Name);
                    return UnknownRequest();
            }
        }

        private IActionResult YesIntent(Alexa.NET.Request.Session session)
        {
            // Check that a session is currently in the createEntryStore
            if (_createEntryStore.Entries.TryGetValue(session.SessionId, out var entry))
            {
                _logger.LogDebug("Delegating dialogue to {DelegatedActivity");
                return Ok(ResponseBuilder.DialogDelegate(session, new Intent { Name = "AddActivity"}));
            }

            LogSessionNotInStore("YesIntent"); 
            return UnknownRequest();
        }

        private IActionResult NoIntent(Alexa.NET.Request.Session session)
        {
            // Check that a session is currently in the createEntryStore
            if (_createEntryStore.Entries.TryGetValue(session.SessionId, out var entry))
            {   
                string responseText;
                // ToDo: Create proper participant IDs
                if (!_participantsService.AddEntry("5ded84556acef0f6eff6da6f", entry)) {
                    responseText = _configuration["Responses:EntryAddFailure"];
                    LogSkillResponse(responseText);
                    return Ok(ResponseBuilder.Tell(responseText));
                }

                responseText = _configuration["Responses:EntryAdded"];
                LogSkillResponse(responseText);
                return Ok(ResponseBuilder.Tell(responseText));
            }
            
            LogSessionNotInStore("NoIntent");
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

        private void LogSkillResponse(string responseText) {
            _logger.LogDebug("Skill response text : {SkillResponseText}", responseText);
        }
        
        private void LogSessionNotInStore(string intent) {
            _logger.LogDebug("Session not found in createEntryStore : {Intent}", intent);
        }
    }
}