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
                    return UnknownRequest(skillRequest.Session);
            }
        }
        
        private IActionResult LaunchRequest()
        {
            var responseText = _configuration["Responses:LaunchRequest"];
            return Ok(BuildAskResponse(responseText, null));
        }

        private IActionResult IntentRequest(SkillRequest skillRequest)
        {
            var intentRequest = skillRequest.Request as IntentRequest;
            var accessToken = _participantsAuthenticationService.GetHashedString(skillRequest.Session.User.AccessToken);
        
            if (skillRequest.Session.Attributes == null)
                skillRequest.Session.Attributes = new Dictionary<string, object>();

            if(!skillRequest.Session.Attributes.TryGetValue("lastIntent", out var _))
                skillRequest.Session.Attributes["lastIntent"] = intentRequest?.Intent.Name;

            switch (intentRequest?.Intent.Name) 
            {
                case "AMAZON.StopIntent":
                case "SetPin":
                    break;
                default:
                    if(!skillRequest.Session.Attributes.TryGetValue("pin", out var _)) {
                        skillRequest.Session.Attributes["intent"] = intentRequest;
                        return Ok(BuildElicitSlot(_configuration["Responses:SetPin"], "pin", new Intent {Name = "SetPin"}, skillRequest.Session.Attributes));
                    }
                    break;
            }

            return RunIntentRequest(accessToken, skillRequest.Session, intentRequest);
        }

        private IActionResult RunIntentRequest(string accessToken, Session session, IntentRequest intentRequest) {
            _logger.LogDebug("Intent launched : {Intent}", intentRequest?.Intent.Name);

            switch (intentRequest?.Intent.Name)
            {
                case "SetPin":
                    return SetPin(accessToken, session, intentRequest);
                case "CreateEntry":
                    return CreateEntry(accessToken, session, intentRequest);    
                case "AddActivityToEntry":
                    return AddActivityToEntry(session, intentRequest);    
                case "ListEntries":
                    return ListEntries(accessToken, session, intentRequest);
                case "ViewEntry":
                    return ViewEntry(accessToken, session, intentRequest);
                case "DeleteEntry": 
                    return DeleteEntry(accessToken, session, intentRequest);
                case "AMAZON.YesIntent":
                    return YesIntent(session);
                case "AMAZON.NoIntent":
                    return NoIntent(accessToken, session);
                case "AMAZON.NextIntent":
                    return NavigationIntent(session, 1);
                case "AMAZON.PreviousIntent":
                    return NavigationIntent(session, -1);
                case "AMAZON.StopIntent":
                    return StopIntent();
                default:
                    _logger.LogError("{Intent} is not a registered intent", intentRequest?.Intent.Name);
                    return UnknownRequest(session);
            }
        }

        private IActionResult StopIntent()
        {
            return Ok(ResponseBuilder.Tell("Bye bye."));
        }

        private IActionResult SetPin(string accessToken, Session session, IntentRequest intentRequest)
        {
            var pin = intentRequest.Intent.Slots["pin"].Value;
            _logger.LogDebug(_participantsAuthenticationService.GetHashedString(pin));
            if (!_participantsService.CheckPin(accessToken, _participantsAuthenticationService.GetHashedString(pin)))
                return Ok(BuildElicitSlot(_configuration["Responses:IncorrectPin"], "pin", intentRequest.Intent, session.Attributes));


            var lastIntent = (string) session.Attributes["lastIntent"];
            var intentJObject = (JObject) session.Attributes["intent"];
            var intent = intentJObject.ToObject<IntentRequest>();
            session.Attributes = new Dictionary<string, object>
            {
                {"pin", pin},
                {"lastIntent", lastIntent}
            };

            return lastIntent == "SetPin" ? Ok(BuildAskResponse(_configuration["Responses:Prompt"], session)) : RunIntentRequest(accessToken, session, intent);
        }


        private IActionResult CreateEntry(string accessToken, Session session, IntentRequest intentRequest)
        {
            session.Attributes["lastIntent"] = "CreateEntry";

            var name = intentRequest.Intent.Slots["name"].Value.ToLower();
            if(CheckEntryExists(accessToken, name))
            {
                return Ok(BuildElicitSlot(string.Format(_configuration["Responses:CreateEntryAlreadyExists"], name), "name"));
            }  
            else 
            {
                var mood = intentRequest.Intent.Slots["mood"].Value;
                session.Attributes["mood"] = mood;
                session.Attributes["rating"] = intentRequest.Intent.Slots["rating"].Value;
                session.Attributes["name"] = name;
                session.Attributes["activities"] = new JArray();

                var responseText = string.Format(_configuration["Responses:CreateEntry"], mood);
                var skillResponse = BuildAskResponse(responseText, session);

                return Ok(skillResponse);
            }
        }
        private IActionResult AddActivityToEntry(Session session, IntentRequest intentRequest)
        {
            session.Attributes["lastIntent"] = "AddActivityToEntry";

            if (session.Attributes == null)
                return UnknownRequest(session);

            if ((string) session.Attributes["lastIntent"] == "CreateEntry" || (string) session.Attributes["lastIntent"] == "AddActivityToEntry") 
            {
                var activities = (JArray) session.Attributes["activities"];
                activities.Add(intentRequest.Intent.Slots["activity"].Value);
                
                return Ok(BuildAskResponse(_configuration["Responses:AddActivityToEntry"], session));
            }
            
            return UnknownRequest(session);
           
        }
        private IActionResult DeleteEntry(string accessToken, Session session, IntentRequest intentRequest)
        {
            session.Attributes["lastIntent"] = "DeleteEntry";

            var name =  intentRequest.Intent.Slots["name"].Value.ToLower();

            if(intentRequest.Intent.ConfirmationStatus.Equals("NONE"))
            {
                var entry = _participantsService.GetEntry(accessToken, name);
                
                if (entry == null)
                    return Ok(BuildAskResponse(string.Format(_configuration["Responses:EntryNotFound"] + " " + _configuration["Responses:Prompt"], name), session));

                return Ok(ResponseBuilder.DialogConfirmIntent(new PlainTextOutputSpeech(string.Format(_configuration["Responses:DeleteEntryConfirmation"], name))));          
            } 
            else if(intentRequest.Intent.ConfirmationStatus.Equals("CONFIRMED"))
            {
                var deleted = _participantsService.DeleteEntry(accessToken, name);

                string responseText;
                
                if(!deleted)
                    responseText = "Hmm... I couldn't deleted that entry for some reason. Try that again.";
                else
                    responseText = "I've deleted that entry!";

                responseText += $" {_configuration["Responses:Prompt"]}";

                return Ok(BuildAskResponse(responseText, session));
            } else {
                return Ok(BuildAskResponse($"{_configuration["Responses:DeleteEntryDenied"]} {_configuration["Responses:Prompt"]}", session: session));
            }
        }

        private IActionResult ViewEntry(string accessToken, Session session, IntentRequest intentRequest)
        {
            session.Attributes["lastIntent"] = "ViewEntry";

            var entry = _participantsService.GetEntry(accessToken, intentRequest.Intent.Slots["name"].Value.ToLower());
            
            if (entry == null) 
                return Ok(BuildAskResponse(string.Format(_configuration["Responses:EntryNotFound"] + " " + _configuration["Responses:Prompt"], intentRequest.Intent.Slots["name"].Value.ToLower()), session));

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

            responseText += $" {_configuration["Responses:Prompt"]}";

            return Ok(BuildAskResponse(responseText, session));
        }

        private IActionResult NavigationIntent(Session session, int moveBy)
        {
            if (session.Attributes == null)
                return UnknownRequest(session);

            if (!session.Attributes.TryGetValue("lastIntent", out var currentIntentObject))
                return UnknownRequest(session);

            if(!session.Attributes.TryGetValue("page", out var pageObject))
                return UnknownRequest(session);

            if(!session.Attributes.TryGetValue("entries", out var entriesObject))
                return UnknownRequest(session);


            var currentIntent = (string) currentIntentObject;
            var page = (long) pageObject;
            var entriesArray = (JArray) entriesObject;
            var entries = entriesArray.ToObject<List<Entry>>();

            return currentIntent switch
            {
                "ListEntries" =>Ok(GetEntriesFromPage(session, entries, page + moveBy)),
                _ => UnknownRequest(session)
            };
        }

        private IActionResult YesIntent(Session session)
        {
            if (session.Attributes == null)
                return UnknownRequest(session);

            if (!session.Attributes.TryGetValue("lastIntent", out var attributeObject))
                return UnknownRequest(session);

            var currentIntent = (string) attributeObject;

            switch (currentIntent) 
            {
                case "CreateEntry":
                case "AddActivityToEntry":
                    return Ok(ResponseBuilder.DialogDelegate(session, new Intent {Name = "AddActivityToEntry"}));
                default:
                    return Ok(UnknownRequest(session));

            }
        }

        private IActionResult NoIntent(string accessToken, Session session)
        {
            if (session.Attributes == null)
                return UnknownRequest(session);

            if (!session.Attributes.TryGetValue("lastIntent", out var attributeObject))
                return UnknownRequest(session);


            var currentIntent = (string) attributeObject;

            _logger.LogDebug(currentIntent);
            switch (currentIntent) 
            {
                case "CreateEntry":
                case "AddActivityToEntry":
                    return AddEntryToDatabase(accessToken, session);
                default:
                     return UnknownRequest(session);
            }
            
        }

        private IActionResult AddEntryToDatabase(string accessToken, Session session) {
            var activitiesArray = (JArray) session.Attributes["activities"];
            var entry = new Entry
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = (string) session.Attributes["name"],
                Mood = (string) session.Attributes["mood"],
                Rating = (string) session.Attributes["rating"],
                Activities = activitiesArray.ToObject<List<string>>()
            };

            var responseText = !_participantsService.AddEntry(accessToken, (string) session.Attributes["pin"], entry)
                ? _configuration["Responses:EntryAddFailure"]
                : _configuration["Responses:EntryAdded"];

            responseText += $" {_configuration["Responses:Prompt"]}";
            
            session.Attributes = new Dictionary<string, object>
            {
                {"pin", session.Attributes["pin"]},
                {"lastIntent", session.Attributes["lastIntent"]}
            };

            return Ok(BuildAskResponse(responseText, session));
        }

        private bool CheckEntryExists(string accessToken, string name) {
            if(_participantsService.GetEntry(accessToken, name) != null) 
                return true;

            return false;
        }

    
        private IActionResult UnknownRequest(Session session){
            return Ok(BuildAskResponse(_configuration["Responses:UnknownRequest"], session));
        }

        

        private IActionResult ListEntries(string accessToken, Session session, IntentRequest intentRequest)
        {
            session.Attributes["lastIntent"] = "ListEntries";

            string mood = null;

            if (intentRequest.Intent.Slots.TryGetValue("mood", out var slot))
                mood = slot.Value;

            var entries = _participantsService.GetEntries(accessToken, mood);
            SkillResponse skillResponse = GetEntriesFromPage(session, entries);

            return Ok(skillResponse);
        }

        private SkillResponse GetEntriesFromPage(Session session, List<Entry> entries, long page = 1)
        {
            if(page < 1)
                page = 1;

            var lastPage = Math.Ceiling((float) entries.Count / 3);
            
            if(page > lastPage)
                return BuildAskResponse(_configuration["Responses:ListEntriesEmpty"], session: session);

            var index = (int) (3 * page) - 3;
            var count = 3;
            var entriesInPage = entries.Count - index;

            if(entriesInPage < count)
                count = entriesInPage;  
                
            var entriesResponse = "";
            
            var latestEntries = entries.GetRange(index, count);

            var entryNumber = 1;
            foreach (var entry in latestEntries)
            {
                entriesResponse += $"<say-as interpret-as=\"ordinal\">{entryNumber}</say-as> entry: {entry.Name}... ";
                entryNumber++;
            }
        
            session.Attributes["entries"] = entries;
            session.Attributes["page"] = page;
            
            var skillResponse = BuildAskResponse($"Page {page} out of {lastPage}. {entriesResponse} {_configuration["Responses:Prompt"]}", session: session);

            return skillResponse;
        }

        private SkillResponse BuildElicitSlot(string message, string slot, Intent intent = null, Dictionary<string, object> sessionAttributes = null){
              var skillResponse = ResponseBuilder.DialogElicitSlot(new PlainTextOutputSpeech(message), slot, intent);
              
              if (sessionAttributes != null)
                  skillResponse.SessionAttributes = sessionAttributes;

              return skillResponse;
        }
        
        private SkillResponse BuildAskResponse(string message, Session session)
        {
            message = "<speak>" + message + "</speak>";

            var speech = new SsmlOutputSpeech(message);
            var repromptSpeech = new SsmlOutputSpeech(message);
            var reprompt = new Reprompt {OutputSpeech = repromptSpeech};

            var skillResponse = ResponseBuilder.Ask(speech, reprompt, session);
            return skillResponse;
        }
    }
}