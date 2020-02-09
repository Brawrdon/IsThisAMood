using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel.Client;
using IsThisAMood.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IsThisAMood.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class AuthController : Controller
    {

        private readonly SignInManager<IdentityUser> _signInManager;

        public AuthController(SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult Login([FromQuery(Name = "client_id")] string clientId,
            [FromQuery(Name = "redirect_uri")] string redirectUri,
            [FromQuery(Name = "response_type")] string responseType,
            [FromQuery(Name = "scope")] string scope,
            [FromQuery(Name = "state")] string state)
        {
            return View(new LoginViewModel
            {
                ClientId = clientId, RedirectUri = redirectUri, ResponseType = responseType, Scope = scope,
                State = state
            });
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromForm] LoginViewModel viewModel)
        {
            var result = await _signInManager.PasswordSignInAsync(viewModel.Username, viewModel.Password, false, false);
            if (result.Succeeded)
            {
                // Get Auth token
                var requestUrl = new RequestUrl("http://localhost:6000/connect/authorize");
                var url = requestUrl.CreateAuthorizeUrl(
                    clientId: viewModel.ClientId,
                    responseType: viewModel.ResponseType,
                    redirectUri: viewModel.RedirectUri,
                    scope: viewModel.Scope,
                    state: viewModel.State);

                return Redirect(url);
            }

            return View();
        }
    }

    public class LoginViewModel
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
