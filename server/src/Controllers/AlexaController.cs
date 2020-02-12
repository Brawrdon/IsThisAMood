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
using Newtonsoft.Json.Linq;

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


        public AlexaController(ILogger<AlexaController> logger, IConfiguration configuration, IParticipantsService participantsService, IParticipantsAuthenticationService participantsAuthenticationService)
        {
            _logger = logger;
            _configuration = configuration;
            _participantsService = participantsService;
            _participantsAuthenticationService = participantsAuthenticationService;
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
            if(session.Attributes == null)
                return UnknownRequest();

            if(!session.Attributes.TryGetValue("currentIntent", out var attributeObject))
                return UnknownRequest();
            
            var currentIntent = (string) attributeObject;

            switch(currentIntent) {
                case "CreateEntry":
                    return Ok(ResponseBuilder.DialogDelegate(session, new Intent { Name = "AddActivity"}));
                default:
                    return UnknownRequest();
            }
            
        }


        private IActionResult NoIntent(Session session)
        {
            var activitiesArray = (JArray) session.Attributes["activities"];
            var entry = new Entry
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = (string) session.Attributes["name"],
                Mood = (string) session.Attributes["mood"], 
                Rating = int.Parse((string) session.Attributes["rating"]),
                Activities = activitiesArray.ToObject<List<string>>()
            };

            string responseText;
            if (!_participantsService.AddEntry(_participantsAuthenticationService.GetHashedAccessToken(session.User.AccessToken), entry))
                responseText = _configuration["Responses:EntryAddFailure"];
            else
                responseText = _configuration["Responses:EntryAdded"];

            return Ok(ResponseBuilder.Tell(responseText));
            
            
        }
        private IActionResult UnknownRequest()
        {
            return Ok(BuildAskResponse(_configuration["Responses:UnknownRequest"]));
        }

        private IActionResult CreateEntry(string sessionId, IntentRequest createEntryRequest)
        {
            var responseText = _configuration["Responses:FirstActivityRequest"];
            var skillResponse = BuildAskResponse(responseText);
            skillResponse.SessionAttributes = new Dictionary<string, object> {
                {"currentIntent", "CreateEntry"},
                {"name", createEntryRequest.Intent.Slots["name"].Value},
                {"mood", createEntryRequest.Intent.Slots["mood"].Value},
                {"rating", createEntryRequest.Intent.Slots["rating"].Value},
                {"activities", new JArray()}
            };
    
            return Ok(skillResponse);
        }

        private IActionResult AddActivity(Session session, IntentRequest intentRequest)
        {
            if(session.Attributes == null)
                return UnknownRequest();

            if((string) session.Attributes["currentIntent"] != "CreateEntry")
                return UnknownRequest();
                
            var activities = (JArray) session.Attributes["activities"]; 
            activities.Add(intentRequest.Intent.Slots["activity"].Value);
            
            return Ok(BuildAskResponse(_configuration["Responses:ActivityRequest"], session: session));
        }

        private SkillResponse BuildTellResponse(string message)
        {  
            var speech = new PlainTextOutputSpeech(message);
            var skillResponse = ResponseBuilder.Tell(speech);
            LogSkillResponse(skillResponse);
            return skillResponse;
        }
        private SkillResponse BuildAskResponse(string message, string repromptMessage = null, Session session = null)
        {
            if (repromptMessage == null)
                repromptMessage = message;
            
            var speech = new PlainTextOutputSpeech(message);
            var repromptSpeech = new PlainTextOutputSpeech(repromptMessage);
            var reprompt = new Reprompt { OutputSpeech = repromptSpeech };

            var skillResponse = ResponseBuilder.Ask(speech, reprompt, session);
            
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