using System.Collections.Generic;
using IsThisAMood.Models.Database;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace IsThisAMood.Services
{
    public interface IParticipantsService
    {
        List<Participant> GetParticipants();
        Participant GetParticipant(string accessToken);
        bool AddEntry(string accessToken, Entry entry);
        bool SetAccessToken(string participantId, string accessToken);
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

        public Participant GetParticipant(string accessToken)
        {
            return _participants.Find(participant => participant.AccessToken == accessToken).FirstOrDefault();
        }

        
        public bool AddEntry(string accessToken, Entry entry)
        {
            var builder = Builders<Participant>.Update; 
            var update = builder.Push("Entries", entry);

            var updateResult = _participants.UpdateOne(participant => participant.AccessToken == accessToken, update);

            if(!updateResult.IsAcknowledged) {
                _logger.LogError("Attempting to insert entry {EntryID} was not acknowledged", entry.Id);
                return false;
            }
                
            if(updateResult.ModifiedCount != 1) {
                _logger.LogError("Modified count when attempting to insert entry {EntryID} was {ModifiedCount}", entry.Id, updateResult.ModifiedCount);
                return false;    
            }
            
            _logger.LogDebug("Entry {EntryID} was added", entry.Id);
            
            return true;
        }

        public bool SetAccessToken(string participantId, string accessToken)
        {
            var builder = Builders<Participant>.Update; 
            var update = builder.Set(participant => participant.AccessToken, accessToken);

            var updateResult = _participants.UpdateOne(participant => participant.Id.Equals(participantId), update);

            if(!updateResult.IsAcknowledged) {
                _logger.LogError("Attempting to update participant {ParticipantID}'s access token was not acknowledged", participantId);
                return false;
            }
                
            if(updateResult.ModifiedCount != 1) {
                _logger.LogError("Modified count when attempting to update access token for participant {ParticipantID} was {ModifiedCount}", participantId, updateResult.ModifiedCount);
                return false;    
            }
            
            _logger.LogDebug("Participant {ParticipantID}'s access token was updated", participantId);
            
            return true;

        }

        
    }
}