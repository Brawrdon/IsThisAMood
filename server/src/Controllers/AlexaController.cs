using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IConfiguration _configuration;
        private readonly ILogger<AlexaController> _logger;
        private readonly IParticipantsAuthenticationService _participantsAuthenticationService;
        private readonly IParticipantsService _participantsService;


        public AlexaController(ILogger<AlexaController> logger, IConfiguration configuration,
            IParticipantsService participantsService,
            IParticipantsAuthenticationService participantsAuthenticationService)
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
                    return CreateEntry(intentRequest);
                case "ListEntries":
                    return ListEntries(skillRequest.Session.User.AccessToken, intentRequest);
                case "AddActivity":
                    return AddActivity(skillRequest.Session, intentRequest);
                case "AMAZON.YesIntent":
                    return YesIntent(skillRequest.Session);
                case "AMAZON.NoIntent":
                    return NoIntent(skillRequest.Session);
                case "AMAZON.NextIntent":
                    return NextIntent(skillRequest.Session);
                default:
                    _logger.LogError("{Intent} is not a registered intent", intentRequest?.Intent.Name);
                    return UnknownRequest();
            }
        }

        private IActionResult NextIntent(Session session)
        {
            if (session.Attributes == null)
                return UnknownRequest();

            if (!session.Attributes.TryGetValue("currentIntent", out var currentIntentObject))
                return UnknownRequest();

            if(!session.Attributes.TryGetValue("page", out var pageObject))
                return UnknownRequest();

            if(!session.Attributes.TryGetValue("entries", out var entriesObject))
                return UnknownRequest();


            var currentIntent = (string) currentIntentObject;
            var page = (long) pageObject;
            var entriesArray = (JArray) entriesObject;
            var entries = entriesArray.ToObject<List<Entry>>();

            return currentIntent switch
            {
                "ListEntries" => Ok(GetEntriesFromPage(page + 1, entries)),
                _ => UnknownRequest()
            };
        }

        private IActionResult YesIntent(Session session)
        {
            if (session.Attributes == null)
                return UnknownRequest();

            if (!session.Attributes.TryGetValue("currentIntent", out var attributeObject))
                return UnknownRequest();

            var currentIntent = (string) attributeObject;

            return currentIntent switch
            {
                "CreateEntry" => Ok(ResponseBuilder.DialogDelegate(session, new Intent {Name = "AddActivity"})),
                _ => UnknownRequest()
            };
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

            var responseText = !_participantsService.AddEntry(
                _participantsAuthenticationService.GetHashedAccessToken(session.User.AccessToken), entry)
                ? _configuration["Responses:EntryAddFailure"]
                : _configuration["Responses:EntryAdded"];

            return Ok(BuildTellResponse(responseText));
        }

        private IActionResult UnknownRequest()
        {
            return Ok(BuildAskResponse(_configuration["Responses:UnknownRequest"]));
        }

        private IActionResult CreateEntry(IntentRequest createEntryRequest)
        {
            var responseText = _configuration["Responses:FirstActivityRequest"];
            var skillResponse = BuildAskResponse(responseText);
            skillResponse.SessionAttributes = new Dictionary<string, object>
            {
                {"currentIntent", "CreateEntry"},
                {"name", createEntryRequest.Intent.Slots["name"].Value},
                {"mood", createEntryRequest.Intent.Slots["mood"].Value},
                {"rating", createEntryRequest.Intent.Slots["rating"].Value},
                {"activities", new JArray()}
            };

            return Ok(skillResponse);
        }

        private IActionResult ListEntries(string accessToken, IntentRequest intentRequest)
        {
            string mood = null;

            if (intentRequest.Intent.Slots.TryGetValue("mood", out var slot))
                mood = slot.Value;

            var entries = _participantsService.GetEntries(_participantsAuthenticationService.GetHashedAccessToken(accessToken), mood);
            SkillResponse skillResponse = GetEntriesFromPage(1, entries);

            return Ok(skillResponse);
        }

        private SkillResponse GetEntriesFromPage(long page, List<Entry> entries)
        {
            var lastPage = Math.Ceiling((float) entries.Count / 3);
            
            if(page > lastPage)
                return BuildAskResponse(_configuration["Responses:ListEntriesEmpty"]);

            var index = (int) (3 * page) - 3;
            var count = 3;

            if(entries.Count - index < count)
                count = entries.Count - index;  
                
            var entriesRespose = "";
            
            var latestEntries = entries.GetRange(index, count);

            var entryNumber = 1;
            foreach (var entry in latestEntries)
            {
                entriesRespose += (" " + NumberToText(entryNumber) + ": " + entry.Name + "...");
                entryNumber++;
            }

            var skillResponse = BuildAskResponse(_configuration["Responses:ViewEntryRequestBegin"] + entriesRespose + _configuration["Responses:ViewEntryRequestEnd"]);

            skillResponse.SessionAttributes = new Dictionary<string, object> {
                 {"currentIntent", "ListEntries"},
                 {"entries", entries},
                 {"page", page}
            };
            return skillResponse;
        }

        private string NumberToText(int number) {
            switch(number) {
                case 1:
                    return "one";
                case 2:
                    return "two";
                case 3:
                    return "three";
                default:
                    return "";
            }
        }

        private IActionResult AddActivity(Session session, IntentRequest intentRequest)
        {
            if (session.Attributes == null)
                return UnknownRequest();

            if ((string) session.Attributes["currentIntent"] != "CreateEntry")
                return UnknownRequest();

            var activities = (JArray) session.Attributes["activities"];
            activities.Add(intentRequest.Intent.Slots["activity"].Value);

            return Ok(BuildAskResponse(_configuration["Responses:ActivityRequest"], session: session));
        }

        private SkillResponse BuildTellResponse(string message)
        {
            var speech = new PlainTextOutputSpeech(message);
            var skillResponse = ResponseBuilder.Tell(speech);
            return skillResponse;
        }

        private SkillResponse BuildAskResponse(string message, string repromptMessage = null, Session session = null)
        {
            if (repromptMessage == null)
                repromptMessage = message;

            var speech = new PlainTextOutputSpeech(message);
            var repromptSpeech = new PlainTextOutputSpeech(repromptMessage);
            var reprompt = new Reprompt {OutputSpeech = repromptSpeech};

            var skillResponse = ResponseBuilder.Ask(speech, reprompt, session);
            return skillResponse;
        }
    }
}