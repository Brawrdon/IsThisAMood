using IsThisAMood.Models.Responses;
using IsThisAMood.Models.Requests;
using IsThisAMood.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IsThisAMood.Controllers
{
    public class LoginController : Controller
    {
        private readonly ILogger<AlexaController> _logger;
        private readonly IParticipantsAuthenticationService _participantsAuthenticationService;
        private readonly IParticipantsService _participantsService;

        public LoginController(ILogger<AlexaController> logger,
            IParticipantsAuthenticationService participantsAuthenticationService,
            IParticipantsService participantsService)
        {
            _logger = logger;
            _participantsAuthenticationService = participantsAuthenticationService;
            _participantsService = participantsService;
        }

        [HttpGet]
        [Route("[controller]")]
        public IActionResult Login([FromQuery(Name = "client_id")] string clientId,
            [FromQuery(Name = "redirect_uri")] string redirectUri,
            [FromQuery(Name = "response_type")] string responseType,
            [FromQuery] string scope,
            [FromQuery] string state,
            [FromQuery] bool fail = false)
        {

            ViewData["fail"] = fail;
            return View("LoginForm", new LoginFormModel
            {
                ClientId = clientId, RedirectUri = redirectUri, ResponseType = responseType, Scope = scope,
                State = state
            });
        }

        [HttpPost]
        [Route("/auth/token")]
        public IActionResult GetAuthenticationToken([FromForm(Name = "grant_type")] string grantType,
            [FromForm] string code,
            [FromForm(Name = "client_id")] string clientId,
            [FromForm(Name = "client_secret")] string clientSecret)
        {
            // ToDo: Check clientId and clientSecret

            var token = _participantsAuthenticationService.CreateAccessToken(code, true);

            //ToDo: Deal with failures

            var accesToken = new AccessToken
            {
                Token = token,
                Type = "bearer"
            };

            return Ok(accesToken);
        }


        [HttpPost]
        [Route("[controller]")]
        public IActionResult Token([FromForm] LoginFormModel loginForm)
        {
            if (!_participantsAuthenticationService.Authenticate(loginForm.Email, loginForm.Pin))
            {
                  return Redirect(Request.Headers["Referer"].ToString() + "&fail=true");
            }

            var code = _participantsAuthenticationService.CreateAuthorisationCode(loginForm.Email);
            return Redirect(loginForm.RedirectUri + "?state=" + loginForm.State + "&code=" + code);
        }

        [HttpPost]
        [Route("/signup")]
        public IActionResult SignUp([FromBody] AccountRequest account) 
        {
            var pin = _participantsAuthenticationService.GetHashedString(account.Pin);
            var email = _participantsAuthenticationService.GetHashedString(account.Email);
            _participantsService.AddParticipant(email, pin);

            var code = _participantsAuthenticationService.CreateAuthorisationCode(account.Email);
            var token = _participantsAuthenticationService.CreateAccessToken(code);

            var accesToken = new AccessToken
            {
                Token = token,
                Type = "bearer"
            };

            return Ok(accesToken);
        }

        [HttpPost]
        [Route("/app/login")]
        public IActionResult login([FromBody] AccountRequest account) 
        {            
            if (_participantsAuthenticationService.Authenticate(account.Email, account.Pin)) {
                var code = _participantsAuthenticationService.CreateAuthorisationCode(account.Email);
                var token = _participantsAuthenticationService.CreateAccessToken(code);

                var accesToken = new AccessToken
                {
                    Token = token,
                    Type = "bearer"
                };

                return Ok(accesToken);

            }

            return NotFound();

        }
    }

    public class LoginFormModel
    {
        public string Email { get; set; }
        public string Pin { get; set; }
        public string ClientId { get; set; }
        public string RedirectUri { get; set; }
        public string ResponseType { get; set; }
        public string Scope { get; set; }
        public string State { get; set; }
    }
}