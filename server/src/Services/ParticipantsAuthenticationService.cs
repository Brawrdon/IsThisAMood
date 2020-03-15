using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace IsThisAMood.Services
{
    public class AuthorisationStore
    {
        public readonly Dictionary<string, string> Codes;

        public AuthorisationStore()
        {
            Codes = new Dictionary<string, string>();
        }
    }

    public interface IParticipantsAuthenticationService
    {
        bool Authenticate(string email, string pin);
        string CreateAuthorisationCode(string email);
        string CreateAccessToken(string email, bool alexa = false);
        string GetHashedString(string stringToHash);
    }

    public class ParticipantsAuthenticationService : IParticipantsAuthenticationService
    {
        private readonly AuthorisationStore _authorisationStore;
        private readonly ILogger<ParticipantsAuthenticationService> _logger;
        private readonly IParticipantsService _participantsService;

        public ParticipantsAuthenticationService(ILogger<ParticipantsAuthenticationService> logger,
            IParticipantsService participantsService, AuthorisationStore authorisationStore)
        {
            _logger = logger;
            _participantsService = participantsService;
            _authorisationStore = authorisationStore;
        }

        public bool Authenticate(string email, string pin)
        {
            email = GetHashedString(email);
            var participant = _participantsService.GetParticipant(email);

            if (participant == null)
            {
                _logger.LogDebug("User {UserName} doesn't exist", email);
                return false;
            }

            //ToDo: Hash pins
            if (email == participant.Email && GetHashedString(pin) == participant.Pin)
            {
                _logger.LogDebug("{UserName} authenticated", email);
                return true;
            }

            _logger.LogDebug("Incorrect pin for {UserName}", email);
            return false;
        }

        public string CreateAccessToken(string code, bool alexa = false)
        {
            var accessToken = Guid.NewGuid().ToString();
            var hashedToken = GetHashedString(accessToken);

            if (!_authorisationStore.Codes.TryGetValue(code, out var email))
                return null;

            var participant = _participantsService.GetParticipant(email);

            if (participant == null)
                return null;

            if (!_participantsService.SetAccessToken(participant.Email, hashedToken, alexa))
                return null;

            return accessToken;
        }

        public string GetHashedString(string stringToHash)
        {
            using var algorithm = SHA256.Create();
            var hashedBytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(stringToHash));
            return BitConverter.ToString(hashedBytes);
        }

        public string CreateAuthorisationCode(string email)
        {
            var code = Guid.NewGuid().ToString();
            _authorisationStore.Codes.Add(code, GetHashedString(email));
            return code;
        }
    }
}