using IsThisAMood.Models.Responses;
using IsThisAMood.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IsThisAMood.Controllers
{
    public class LoginController : Controller
    {
        private readonly ILogger<AlexaController> _logger;
        private readonly IParticipantsAuthenticationService _participantsAuthenticationService;

        public LoginController(ILogger<AlexaController> logger,
            IParticipantsAuthenticationService participantsAuthenticationService)
        {
            _logger = logger;
            _participantsAuthenticationService = participantsAuthenticationService;
        }

        [HttpGet]
        [Route("[controller]")]
        public IActionResult Login([FromQuery(Name = "client_id")] string clientId,
            [FromQuery(Name = "redirect_uri")] string redirectUri,
            [FromQuery(Name = "response_type")] string responseType,
            [FromQuery] string scope,
            [FromQuery] string state)
        {
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

            var token = _participantsAuthenticationService.CreateAccessToken(code);

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
            if (!_participantsAuthenticationService.Authenticate(loginForm.Username, loginForm.Password))
            {
                return RedirectToAction("Login", new LoginFormModel
                {
                    ClientId = loginForm.ClientId, RedirectUri = loginForm.RedirectUri,
                    ResponseType = loginForm.ResponseType, Scope = loginForm.Scope,
                    State = loginForm.State
                });
            }

            var code = _participantsAuthenticationService.CreateAuthorisationCode(loginForm.Username);
            return Redirect(loginForm.RedirectUri + "?state=" + loginForm.State + "&code=" + code);
        }
    }

    public class LoginFormModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string ClientId { get; set; }
        public string RedirectUri { get; set; }
        public string ResponseType { get; set; }
        public string Scope { get; set; }
        public string State { get; set; }
    }
}