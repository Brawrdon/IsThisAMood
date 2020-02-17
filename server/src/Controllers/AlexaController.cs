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
                case "ViewEntry":
                    return ViewEntry(skillRequest.Session.User.AccessToken, intentRequest);
                case "AddActivity":
                    return AddActivity(skillRequest.Session, intentRequest);
                case "AMAZON.YesIntent":
                    return YesIntent(skillRequest.Session);
                case "AMAZON.NoIntent":
                    return NoIntent(skillRequest.Session);
                case "AMAZON.NextIntent":
                    return NavigiationIntent(skillRequest.Session, 1);
                case "AMAZON.PreviousIntent":
                    return NavigiationIntent(skillRequest.Session, -1);
                default:
                    _logger.LogError("{Intent} is not a registered intent", intentRequest?.Intent.Name);
                    return UnknownRequest();
            }
        }


        private IActionResult ViewEntry(string accessToken, IntentRequest viewEntryRequest)
        {
            var entry = _participantsService.GetEntry(_participantsAuthenticationService.GetHashedAccessToken(accessToken), viewEntryRequest.Intent.Slots["name"].Value);
            
            if (entry == null) 
                return Ok(BuildTellResponse(_configuration["Responses:EntryNotFound"]));

            var responseText = $"Here's the entry for {entry.Name}. You felt {entry.Mood} and gave it a rating of {entry.Rating}.";

            var activities = entry.Activities.ToList();
            if (activities.Count == 0)
            {
              responseText += $" You didn't include any activities."; 
            } 
            else 
            {
                responseText += " You said the following activities contributed to your mood:";
                foreach(var activity in activities) 
                {
                    responseText += $" {activity}...";
                }
            }

            return Ok(BuildTellResponse(responseText));
        }

        private IActionResult NavigiationIntent(Session session, int moveBy)
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
                "ListEntries" =>Ok(GetEntriesFromPage(entries, page + moveBy)),
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
            SkillResponse skillResponse = GetEntriesFromPage(entries);

            return Ok(skillResponse);
        }

        private SkillResponse GetEntriesFromPage(List<Entry> entries, long page = 1)
        {
            if(page < 1)
                page = 1;

            var lastPage = Math.Ceiling((float) entries.Count / 3);
            
            if(page > lastPage)
                return BuildAskResponse(_configuration["Responses:ListEntriesEmpty"]);

            var index = (int) (3 * page) - 3;
            var count = 3;
            var entriesInPage = entries.Count - index;

            if(entriesInPage < count)
                count = entriesInPage;  
                
            var entriesRespose = "";
            
            var latestEntries = entries.GetRange(index, count);

            var entryNumber = 1;
            foreach (var entry in latestEntries)
            {
                entriesRespose += $"<say-as interpret-as=\"ordinal\">{entryNumber}</say-as> entry: {entry.Name}... ";
                entryNumber++;
            }
            
            var skillResponse = BuildAskResponse($"Page {page} out of {lastPage}. " + entriesRespose + _configuration["Responses:ListEntriesEnd"]);

            skillResponse.SessionAttributes = new Dictionary<string, object> {
                 {"currentIntent", "ListEntries"},
                 {"entries", entries},
                 {"page", page}
            };
            return skillResponse;
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
            message = "<speak>" + message + "</speak>";
            var speech = new SsmlOutputSpeech(message);
            var skillResponse = ResponseBuilder.Tell(speech);
            return skillResponse;
        }

        private SkillResponse BuildAskResponse(string message, string repromptMessage = null, Session session = null)
        {
            message = "<speak>" + message + "</speak>";

            if (repromptMessage == null)
                repromptMessage = message;

            var speech = new SsmlOutputSpeech(message);
            var repromptSpeech = new SsmlOutputSpeech(repromptMessage);
            var reprompt = new Reprompt {OutputSpeech = repromptSpeech};

            var skillResponse = ResponseBuilder.Ask(speech, reprompt, session);
            return skillResponse;
        }
    }
}