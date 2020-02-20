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
        bool Authenticate(string username, string password);
        string CreateAuthorisationCode(string username);
        string CreateAccessToken(string username);
        string GetHashedString(string accessToken);
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

        public bool Authenticate(string username, string password)
        {
            var participant = _participantsService.GetParticipant(username);

            if (participant == null)
            {
                _logger.LogDebug("User {UserName} doesn't exist", username);
                return false;
            }

            //ToDo: Hash passwords
            if (username == participant.Username && password == participant.Password)
            {
                _logger.LogDebug("{UserName} authenticated", username);
                return true;
            }

            _logger.LogDebug("Incorrect password for {UserName}", username);
            return false;
        }

        public string CreateAccessToken(string code)
        {
            var accessToken = Guid.NewGuid().ToString();
            var hashedToken = GetHashedString(accessToken);

            if (!_authorisationStore.Codes.TryGetValue(code, out var username))
                return null;

            var participant = _participantsService.GetParticipant(username);

            if (participant == null)
                return null;

            if (!_participantsService.SetAccessToken(participant.Username, hashedToken))
                return null;

            return accessToken;
        }

        public string GetHashedString(string accessToken)
        {
            using var algorithm = SHA256.Create();
            var hashedBytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(accessToken));
            return Encoding.UTF8.GetString(hashedBytes);
        }

        public string CreateAuthorisationCode(string username)
        {
            var code = Guid.NewGuid().ToString();
            _authorisationStore.Codes.Add(code, username);
            return code;
        }
    }
}