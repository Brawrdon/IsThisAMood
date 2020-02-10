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
        private readonly IParticipantsAuthenticationService _participantsAuthenticationService;
        private readonly CreateEntryStore _createEntryStore;
        private readonly AlexaSessionStore _alexaSessionStore;

        public AlexaController(ILogger<AlexaController> logger, IConfiguration configuration, IParticipantsService participantsService, IParticipantsAuthenticationService participantsAuthenticationService, CreateEntryStore createEntryStore)
        {
            _logger = logger;
            _configuration = configuration;
            _participantsService = participantsService;
            _participantsAuthenticationService = participantsAuthenticationService;
            _createEntryStore = createEntryStore;
        }
        
        [HttpPost]
        public IActionResult ReceiveRequest(SkillRequest skillRequest)
        {
            string skillId = skillRequest.Context.System.Application.ApplicationId;
            if (!skillId.Equals(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID")))
            {
                _logger.LogWarning("Incorrect skill ID : {SkillID}", skillId);
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
            return Ok(BuildAskResponse(responseText));
        }

        private IActionResult IntentRequest(SkillRequest skillRequest)
        {
            var intentRequest = skillRequest.Request as IntentRequest;

            _logger.LogDebug("Intent launched : {Intent}", intentRequest?.Intent.Name);

            switch (intentRequest?.Intent.Name)
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
                    _logger.LogError("{Intent} is not a registered intent", intentRequest?.Intent.Name);
                    return UnknownRequest();
            }
        }

        private IActionResult YesIntent(Session session)
        {
            // Check that a session is currently in the createEntryStore
            if (_createEntryStore.Entries.TryGetValue(session.SessionId, out _))
            {
                _logger.LogDebug("Delegating dialogue to {DelegatedActivity}");
                return Ok(ResponseBuilder.DialogDelegate(session, new Intent { Name = "AddActivity"}));
            }

            LogSessionNotInStore(session.SessionId, "YesIntent"); 
            return UnknownRequest();
        }

        private IActionResult NoIntent(Session session)
        {
            // Check that a session is currently in the createEntryStore
            if (_createEntryStore.Entries.TryGetValue(session.SessionId, out var entry))
            {   
                string responseText;
                // ToDo: Create proper participant IDs
                if (!_participantsService.AddEntry(_participantsAuthenticationService.GetHashedAccessToken(session.User.AccessToken), entry)) {
                    responseText = _configuration["Responses:EntryAddFailure"];
                    return Ok(ResponseBuilder.Tell(responseText));
                }

                responseText = _configuration["Responses:EntryAdded"];
                return Ok(ResponseBuilder.Tell(responseText));
            }
            
            LogSessionNotInStore(session.SessionId, "NoIntent");
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
            
            if(!_createEntryStore.Entries.TryAdd(sessionId, entry)) {
                _logger.LogWarning("Unable to add session {SessionID} to createEntryStore");
                return Ok(BuildTellResponse(_configuration["Responses:EntryAddFailure"]));
            }
            
            var responseText = _configuration["Responses:FirstActivityRequest"];
            
            return Ok(BuildAskResponse(responseText));
        }

        private IActionResult AddActivity(Session session, IntentRequest intentRequest)
        {
            // Check the session is currently active
            if (_createEntryStore.Entries.TryGetValue(session.SessionId, out var entry) == false)
                return UnknownRequest();
            
            entry.Activities.Add(intentRequest.Intent.Slots["activity"].Value);

            return Ok(BuildAskResponse(_configuration["Responses:ActivityRequest"]));
        }

        private SkillResponse BuildTellResponse(string message)
        {  
            var speech = new PlainTextOutputSpeech(message);
            var skillResponse = ResponseBuilder.Tell(speech);
            LogSkillResponse(skillResponse);
            return skillResponse;
        }
        private SkillResponse BuildAskResponse(string message, string repromptMessage = null)
        {
            if (repromptMessage == null)
                repromptMessage = message;
            
            var speech = new PlainTextOutputSpeech(message);
            var repromptSpeech = new PlainTextOutputSpeech(repromptMessage);
            var reprompt = new Reprompt { OutputSpeech = repromptSpeech };

            var skillResponse = ResponseBuilder.Ask(speech, reprompt);
            LogSkillResponse(skillResponse);
            return skillResponse;
        }

        private void LogSkillResponse(SkillResponse skillResponse) {
            _logger.LogDebug("Skill response : {@SkillResponse}", skillResponse);
        }
        
        private void LogSessionNotInStore(string sessionId, string intent) {
            _logger.LogDebug("Session {SessionID} not found in createEntryStore : {Intent}", sessionId, intent);
        }
    }
}