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

        private SkillRequest _skillRequest;
        private string _accessToken;
        private IntentRequest _intentRequest;

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
            _skillRequest = skillRequest;
            var skillId = skillRequest.Context.System.Application.ApplicationId;
            if (!skillId.Equals(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID")))
            {
                _logger.LogWarning("Incorrect skill ID : {SkillID}", skillId);
                return Unauthorized();
            }

            _logger.LogDebug("Request type : {AlexaRequest}", skillRequest.Request.Type);

            return skillRequest.Request.Type switch
            {
                "LaunchRequest" => LaunchRequest(),
                "IntentRequest" => IntentRequest(),
                _ => UnknownRequest()
            };
        }
        
        private IActionResult LaunchRequest()
        {
            var responseText = _configuration["Responses:LaunchRequest"];
            return Ok(BuildAskResponse(responseText));
        }

        private IActionResult IntentRequest()
        {
            _intentRequest = _skillRequest.Request as IntentRequest;
            _accessToken = _participantsAuthenticationService.GetHashedString(_skillRequest.Session.User.AccessToken);
        
            if (_skillRequest.Session.Attributes == null)
                _skillRequest.Session.Attributes = new Dictionary<string, object>();

            if(!_skillRequest.Session.Attributes.TryGetValue("lastIntent", out _))
                _skillRequest.Session.Attributes["lastIntent"] = _intentRequest?.Intent.Name;

            switch (_intentRequest?.Intent.Name) 
            {
                case "AMAZON.StopIntent":
                case "SetPin":
                    break;
                default:
                    if(!_skillRequest.Session.Attributes.TryGetValue("pin", out var _)) {
                        _skillRequest.Session.Attributes["intent"] = _intentRequest;
                        return Ok(BuildElicitSlot(_configuration["Responses:SetPin"], "pin", new Intent {Name = "SetPin"}));
                    }
                    break;
            }

            return RunIntentRequest();
        }

        private IActionResult RunIntentRequest() {
            _logger.LogDebug("Intent launched : {Intent}", _intentRequest?.Intent.Name);

            switch (_intentRequest?.Intent.Name)
            {
                case "SetPin":
                    return SetPin();
                case "CreateEntry":
                    return CreateEntry();    
                case "AddActivityToEntry":
                    return AddActivityToEntry();    
                case "ListEntries":
                    return ListEntries();
                case "ViewEntry":
                    return ViewEntry();
                case "DeleteEntry": 
                    return DeleteEntry();
                case "AMAZON.YesIntent":
                    return YesIntent();
                case "AMAZON.NoIntent":
                    return NoIntent();
                case "AMAZON.NextIntent":
                    return NavigationIntent(1);
                case "AMAZON.PreviousIntent":
                    return NavigationIntent(-1);
                case "AMAZON.StopIntent":
                    return StopIntent();
                default:
                    _logger.LogError("{Intent} is not a registered intent", _intentRequest?.Intent.Name);
                    return UnknownRequest();
            }
        }

        private IActionResult StopIntent()
        {
            return Ok(ResponseBuilder.Tell("Bye bye."));
        }

        private IActionResult SetPin()
        {
            var pin = _intentRequest.Intent.Slots["pin"].Value;
            _logger.LogDebug(_participantsAuthenticationService.GetHashedString(pin));
            if (!_participantsService.CheckPin(_accessToken, _participantsAuthenticationService.GetHashedString(pin)))
                return Ok(BuildElicitSlot(_configuration["Responses:IncorrectPin"], "pin", new Intent {Name = "SetPin"}));


            var lastIntent = (string) _skillRequest.Session.Attributes["lastIntent"];
            var intentJObject = (JObject) _skillRequest.Session.Attributes["intent"];
            _intentRequest = intentJObject.ToObject<IntentRequest>();
            _skillRequest.Session.Attributes = new Dictionary<string, object>
            {
                {"pin", pin},
                {"lastIntent", lastIntent}
            };

            return lastIntent == "SetPin" ? Ok(BuildAskResponse(_configuration["Responses:Prompt"])) : RunIntentRequest();
        }


        private IActionResult CreateEntry()
        {
            _skillRequest.Session.Attributes["lastIntent"] = "CreateEntry";

            var name = _intentRequest.Intent.Slots["name"].Value.ToLower();
            if(CheckEntryExists(name))
            {
                return Ok(BuildElicitSlot(string.Format(_configuration["Responses:CreateEntryAlreadyExists"], name), "name"));
            }  
            else 
            {
                var mood = _intentRequest.Intent.Slots["mood"].Value;
                _skillRequest.Session.Attributes["mood"] = mood;
                _skillRequest.Session.Attributes["rating"] = _intentRequest.Intent.Slots["rating"].Value;
                _skillRequest.Session.Attributes["name"] = name;
                _skillRequest.Session.Attributes["activities"] = new JArray();

                var responseText = string.Format(_configuration["Responses:CreateEntry"], mood);
                var skillResponse = BuildAskResponse(responseText);

                return Ok(skillResponse);
            }
        }
        private IActionResult AddActivityToEntry()
        {
            _skillRequest.Session.Attributes["lastIntent"] = "AddActivityToEntry";

            if (_skillRequest.Session.Attributes == null)
                return UnknownRequest();

            if ((string) _skillRequest.Session.Attributes["lastIntent"] == "CreateEntry" || (string) _skillRequest.Session.Attributes["lastIntent"] == "AddActivityToEntry") 
            {
                var activities = (JArray) _skillRequest.Session.Attributes["activities"];
                activities.Add(_intentRequest.Intent.Slots["activity"].Value);
                
                return Ok(BuildAskResponse(_configuration["Responses:AddActivityToEntry"]));
            }
            
            return UnknownRequest();
           
        }
        private IActionResult DeleteEntry()
        {
            _skillRequest.Session.Attributes["lastIntent"] = "DeleteEntry";

            var name =  _intentRequest.Intent.Slots["name"].Value.ToLower();

            if(_intentRequest.Intent.ConfirmationStatus.Equals("NONE"))
            {
                var entry = _participantsService.GetEntry(_accessToken, (string) _skillRequest.Session.Attributes["pin"], name);
                
                if (entry == null)
                    return Ok(BuildAskResponse(string.Format(_configuration["Responses:EntryNotFound"] + " " + _configuration["Responses:Prompt"], name)));

                return Ok(ResponseBuilder.DialogConfirmIntent(new PlainTextOutputSpeech(string.Format(_configuration["Responses:DeleteEntryConfirmation"], name))));          
            } 
            else if(_intentRequest.Intent.ConfirmationStatus.Equals("CONFIRMED"))
            {
                var deleted = _participantsService.DeleteEntry(_accessToken, name);

                string responseText;
                
                if(!deleted)
                    responseText = "Hmm... I couldn't deleted that entry for some reason. Try that again.";
                else
                    responseText = "I've deleted that entry!";

                responseText += $" {_configuration["Responses:Prompt"]}";

                return Ok(BuildAskResponse(responseText));
            } else {
                return Ok(BuildAskResponse($"{_configuration["Responses:DeleteEntryDenied"]} {_configuration["Responses:Prompt"]}"));
            }
        }

        private IActionResult ViewEntry()
        {
            _skillRequest.Session.Attributes["lastIntent"] = "ViewEntry";

            var entry = _participantsService.GetEntry(_accessToken, (string) _skillRequest.Session.Attributes["pin"], _intentRequest.Intent.Slots["name"].Value.ToLower());
            
            if (entry == null) 
                return Ok(BuildAskResponse(string.Format(_configuration["Responses:EntryNotFound"] + " " + _configuration["Responses:Prompt"], _intentRequest.Intent.Slots["name"].Value.ToLower())));

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

            return Ok(BuildAskResponse(responseText));
        }

        private IActionResult NavigationIntent(int moveBy)
        {
            if (_skillRequest.Session.Attributes == null)
                return UnknownRequest();

            if (!_skillRequest.Session.Attributes.TryGetValue("lastIntent", out var currentIntentObject))
                return UnknownRequest();

            if(!_skillRequest.Session.Attributes.TryGetValue("page", out var pageObject))
                return UnknownRequest();

            if(!_skillRequest.Session.Attributes.TryGetValue("entries", out var entriesObject))
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

        private IActionResult YesIntent()
        {
            if (_skillRequest.Session.Attributes == null)
                return UnknownRequest();

            if (!_skillRequest.Session.Attributes.TryGetValue("lastIntent", out var attributeObject))
                return UnknownRequest();

            var currentIntent = (string) attributeObject;

            switch (currentIntent) 
            {
                case "CreateEntry":
                case "AddActivityToEntry":
                    return Ok(ResponseBuilder.DialogDelegate(_skillRequest.Session, new Intent {Name = "AddActivityToEntry"}));
                default:
                    return Ok(UnknownRequest());

            }
        }

        private IActionResult NoIntent()
        {
            if (_skillRequest.Session.Attributes == null)
                return UnknownRequest();

            if (!_skillRequest.Session.Attributes.TryGetValue("lastIntent", out var attributeObject))
                return UnknownRequest();


            var currentIntent = (string) attributeObject;

            _logger.LogDebug(currentIntent);
            switch (currentIntent) 
            {
                case "CreateEntry":
                case "AddActivityToEntry":
                    return AddEntryToDatabase();
                default:
                     return UnknownRequest();
            }
            
        }

        private IActionResult AddEntryToDatabase() {
            var activitiesArray = (JArray) _skillRequest.Session.Attributes["activities"];
            var entry = new Entry
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = (string) _skillRequest.Session.Attributes["name"],
                Mood = (string) _skillRequest.Session.Attributes["mood"],
                Rating = (string) _skillRequest.Session.Attributes["rating"],
                Activities = activitiesArray.ToObject<List<string>>()
            };

            var responseText = !_participantsService.AddEntry(_accessToken,  (string) _skillRequest.Session.Attributes["pin"], entry)
                ? _configuration["Responses:EntryAddFailure"]
                : _configuration["Responses:EntryAdded"];

            responseText += $" {_configuration["Responses:Prompt"]}";
            
            _skillRequest.Session.Attributes = new Dictionary<string, object>
            {
                {"pin", _skillRequest.Session.Attributes["pin"]},
                {"lastIntent", _skillRequest.Session.Attributes["lastIntent"]}
            };

            return Ok(BuildAskResponse(responseText));
        }

        private bool CheckEntryExists(string name) {
            if(_participantsService.GetEntry(_accessToken, (string) _skillRequest.Session.Attributes["pin"],  name) != null) 
                return true;

            return false;
        }

    
        private IActionResult UnknownRequest(){
            return Ok(BuildAskResponse(_configuration["Responses:UnknownRequest"]));
        }

        

        private IActionResult ListEntries()
        {
            _skillRequest.Session.Attributes["lastIntent"] = "ListEntries";

            string mood = null;

            if (_intentRequest.Intent.Slots.TryGetValue("mood", out var slot))
                mood = slot.Value;

            var entries = _participantsService.GetEntries(_accessToken, (string) _skillRequest.Session.Attributes["pin"], mood);
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
                
            var entriesResponse = "";
            
            var latestEntries = entries.GetRange(index, count);

            var entryNumber = 1;
            foreach (var entry in latestEntries)
            {
                entriesResponse += $"<say-as interpret-as=\"ordinal\">{entryNumber}</say-as> entry: {entry.Name}... ";
                entryNumber++;
            }
        
            _skillRequest.Session.Attributes["entries"] = entries;
            _skillRequest.Session.Attributes["page"] = page;
            
            var skillResponse = BuildAskResponse($"Page {page} out of {lastPage}. {entriesResponse} {_configuration["Responses:Prompt"]}");

            return skillResponse;
        }

        private SkillResponse BuildElicitSlot(string message, string slot, Intent intent = null){
              var skillResponse = ResponseBuilder.DialogElicitSlot(new PlainTextOutputSpeech(message), slot, intent);
            
              skillResponse.SessionAttributes = _skillRequest.Session.Attributes;

              return skillResponse;
        }
        
        private SkillResponse BuildAskResponse(string message)
        {
            message = "<speak>" + message + "</speak>";

            var speech = new SsmlOutputSpeech(message);
            var repromptSpeech = new SsmlOutputSpeech(message);
            var reprompt = new Reprompt {OutputSpeech = repromptSpeech};

            var skillResponse = ResponseBuilder.Ask(speech, reprompt, _skillRequest.Session);
            return skillResponse;
        }
    }
}