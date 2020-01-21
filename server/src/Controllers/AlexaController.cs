﻿using System;
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
        private readonly IDictionary<string, Entry> _entryStore;

        public AlexaController(ILogger<AlexaController> logger, IConfiguration configuration, IParticipantsService participantsService, IDictionary<string, Entry> entryStore)
        {
            _logger = logger;
            _configuration = configuration;
            _participantsService = participantsService;
            _entryStore = entryStore;
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
            return Ok(BuildAskResponse(_configuration["Responses:LaunchRequest"]));
        }

        
        private IActionResult IntentRequest(SkillRequest skillRequest)
        {
            var intentRequest = skillRequest.Request as IntentRequest;
            
            switch (intentRequest.Intent.Name)
            {
                case "CreateEntry":
                    return CreateEntry(skillRequest.Session.SessionId, intentRequest);
                case "AddActivity":
                    return AddActivity(skillRequest.Session, intentRequest);
                case "AddTitle":
                    return AddTitle(skillRequest.Session.SessionId, intentRequest);
                default:
                    return UnknownRequest();
            }
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
            
            _entryStore.Add(sessionId, entry);
            
            return Ok(BuildAskResponse(_configuration["Responses:ActivitiesRequired"]));
        }

        private IActionResult AddActivity(Session session, IntentRequest intentRequest)
        {
            // Check the session is currently active
            if (_entryStore.TryGetValue(session.SessionId, out var entry) == false)
                return UnknownRequest();

            var activity = intentRequest.Intent.Slots["activity"].Value;

            if (activity.Equals("no"))
                return Ok(ResponseBuilder.DialogDelegate(session, new Intent { Name = "AddTitle"}));
                    
            entry.Activities.Add(intentRequest.Intent.Slots["activity"].Value);

            return Ok(BuildAskResponse(_configuration["Responses:ActivitiesRequest"]));
        }
        
        private IActionResult AddTitle(string sessionId, IntentRequest intentRequest)
        {
            // Check the session is currently active
            if (_entryStore.TryGetValue(sessionId, out var entry) == false)
                return UnknownRequest();
            
            entry.Title = intentRequest.Intent.Slots["title"].Value;
            
            // Complete entry
            // ToDo: Create proper participant IDs
            _participantsService.AddEntry("5ded84556acef0f6eff6da6f", entry);

            return Ok(BuildAskResponse(_configuration["Responses:CreatedEntry"]));
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