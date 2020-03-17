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
        public IActionResult Questionnaire([FromQuery] string code, [FromQuery] string state)
        {
            var token = _participantsAuthenticationService.CreateAccessToken(code);

            var paticipant = _participantsService.GetParticipantFromToken(_participantsAuthenticationService.GetHashedString(token));
            if (paticipant == null)
                return NotFound();

            ViewData["number"] = state;
            ViewData["token"] = token;

            return View("EmotionQuestionnaire", new EmotionQuestionnaireForm
            {
                Token = token
            });
        }

        [HttpPost]
        [Route("[controller]")]
        public IActionResult SumbitQuestionnaire([FromForm] EmotionQuestionnaireForm form, [FromQuery] string number)
        {

            float recognition = ((Reverse(float.Parse(form.Question4)) + float.Parse(form.Question18) + float.Parse(form.Question20) + float.Parse(form.Question21) + Reverse(float.Parse(form.Question22)) + float.Parse(form.Question24)) / 6) * 5;

            var identification = ((Reverse(float.Parse(form.Question1)) + float.Parse(form.Question3) + Reverse(float.Parse(form.Question8)) + float.Parse(form.Question17) + Reverse(float.Parse(form.Question29))) / 5) * 5;

            float communication = ((float.Parse(form.Question6) + float.Parse(form.Question12) + float.Parse(form.Question13) + float.Parse(form.Question15) + float.Parse(form.Question27) + float.Parse(form.Question30) + Reverse(float.Parse(form.Question31))) / 7) * 5;
            
            float context = ((float.Parse(form.Question5) + float.Parse(form.Question7) + Reverse(float.Parse(form.Question10)) + float.Parse(form.Question11) + Reverse(float.Parse(form.Question14)) + Reverse(float.Parse(form.Question16)) + float.Parse(form.Question19) + float.Parse(form.Question28) + float.Parse(form.Question32) + float.Parse(form.Question33)) / 7) * 5;

            float decision = ((float.Parse(form.Question2) + Reverse(float.Parse(form.Question8)) + float.Parse(form.Question23) + Reverse(float.Parse(form.Question25)) + float.Parse(form.Question26)) / 5) * 5;

            _participantsService.AddQuestionnaire(_participantsAuthenticationService.GetHashedString(form.Token), int.Parse(number), recognition, identification, communication, context, decision);
            
            return Ok("Thank you for filling out the questionnaire.");
        }

        int Reverse(float number) 
        {
            switch(number) 
            {
                case 0:
                    return 4;
                case 1:
                    return 3;
                case 2:
                    return 2;
                case 3:
                    return 1;
                case 4:
                    return 0;
                default:
                    return -1;
            }

        }
    }
    
    public class EmotionQuestionnaireForm
    {
        public string Token {get; set;}
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