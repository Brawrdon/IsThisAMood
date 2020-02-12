using System.Collections.Generic;
using IsThisAMood.Models.Database;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace IsThisAMood.Services
{
    public interface IParticipantsService
    {
        List<Participant> GetParticipants();
        Participant GetParticipant(string username);
        bool AddEntry(string accessToken, Entry entry);
        bool SetAccessToken(string username, string accessToken);
        List<Entry> GetEntries(string accessToken, string mood);
    }

    public class ParticipantsService : IParticipantsService
    {
        private readonly ILogger<ParticipantsService> _logger;
        private readonly IMongoCollection<Participant> _participants;

        public ParticipantsService(ILogger<ParticipantsService> logger, IDatabaseSettings settings)
        {
            _logger = logger;
            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);

            _participants = database.GetCollection<Participant>(settings.CollectionName);
        }

        public List<Participant> GetParticipants()
        {
            return _participants.Find(participant => true).ToList();
        }

        public Participant GetParticipant(string username)
        {
            return _participants.Find(participant => participant.Username == username).FirstOrDefault();
        }

        public bool AddEntry(string accessToken, Entry entry)
        {
            var builder = Builders<Participant>.Update;
            var update = builder.Push("Entries", entry);

            var updateResult = _participants.UpdateOne(participant => participant.AccessToken == accessToken, update);

            if (!updateResult.IsAcknowledged)
            {
                _logger.LogError("Attempting to insert entry {EntryID} was not acknowledged", entry.Id);
                return false;
            }

            if (updateResult.ModifiedCount != 1)
            {
                _logger.LogError("Modified count when attempting to insert entry {EntryID} was {ModifiedCount}",
                    entry.Id, updateResult.ModifiedCount);
                return false;
            }

            _logger.LogDebug("Entry {EntryID} was added", entry.Id);

            return true;
        }

        public bool SetAccessToken(string username, string accessToken)
        {
            var builder = Builders<Participant>.Update;
            var update = builder.Set(participant => participant.AccessToken, accessToken);

            var updateResult = _participants.UpdateOne(participant => participant.Username == username, update);

            if (!updateResult.IsAcknowledged)
            {
                _logger.LogError("Attempting to update participant {UserName}'s access token was not acknowledged",
                    username);
                return false;
            }

            if (updateResult.ModifiedCount != 1)
            {
                _logger.LogError(
                    "Modified count when attempting to update access token for participant {UserName} was {ModifiedCount}",
                    username, updateResult.ModifiedCount);
                return false;
            }

            _logger.LogDebug("Participant {UserName}'s access token was updated", username);
            
            return true;
        }

        public List<Entry> GetEntries(string accessToken, string mood = null)
        {
            _logger.LogDebug("{AccessToken}", accessToken);
            var particpant = GetParticipantFromToken(accessToken);
            var entries = particpant.Entries;

            if(mood != null)
                entries = entries.FindAll(x => x.Mood == mood);

            entries.Reverse();
            return entries;
        }

        public Participant GetParticipantFromToken(string accessToken)
        {
            return _participants.Find(participant => participant.AccessToken == accessToken).FirstOrDefault();
        }
    }
}