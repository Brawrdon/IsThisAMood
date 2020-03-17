using IsThisAMood.Models.Responses;
using IsThisAMood.Models.Requests;
using IsThisAMood.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IsThisAMood.Controllers
{
    public class QuestionnaireController : Controller
    {
        private readonly ILogger<QuestionnaireController> _logger;
        private readonly IParticipantsAuthenticationService _participantsAuthenticationService;
        private readonly IParticipantsService _participantsService;

        public QuestionnaireController(ILogger<QuestionnaireController> logger,
            IParticipantsAuthenticationService participantsAuthenticationService,
            IParticipantsService participantsService)
        {
            _logger = logger;
            _participantsAuthenticationService = participantsAuthenticationService;
            _participantsService = participantsService;
        }

        [HttpGet]
        [Route("[controller]")]
        public IActionResult Questionnaire([FromQuery] string token, [FromQuery] string number)
        {
            _logger.LogDebug(token);
            var paticipant = _participantsService.GetParticipantFromToken(_participantsAuthenticationService.GetHashedString(token));
            if (paticipant == null)
                return NotFound();

            ViewData["number"] = number;

            return View("EmotionQuestionnaire", new EmotionQuestionnaireForm
            {
                Token = token
            });
        }
    }
    
    public class EmotionQuestionnaireForm
    {
        public string Token {get; set;}
        public string RedirectUri {get; set;}
        public string Question1 {get; set;}
        public string Question2 {get; set;}
        public string Question3 {get; set;}
        public string Question4 {get; set;}
        public string Question5 {get; set;}
        public string Question6 {get; set;}
        public string Question7 {get; set;}
        public string Question8 {get; set;}
        public string Question9 {get; set;}
        public string Question10 {get; set;}
        public string Question11 {get; set;}
        public string Question12 {get; set;}
        public string Question13 {get; set;}
        public string Question14 {get; set;}
        public string Question15 {get; set;}
        public string Question16 {get; set;}
        public string Question17 {get; set;}
        public string Question18 {get; set;}
        public string Question19 {get; set;}
        public string Question20 {get; set;}
        public string Question21 {get; set;}
        public string Question22 {get; set;}
        public string Question23 {get; set;}
        public string Question24 {get; set;}
        public string Question25 {get; set;}
        public string Question26 {get; set;}
        public string Question27 {get; set;}
        public string Question28 {get; set;}
        public string Question29 {get; set;}
        public string Question30 {get; set;}
        public string Question31 {get; set;}
        public string Question32 {get; set;}
        public string Question33 {get; set;}

        
    }
}