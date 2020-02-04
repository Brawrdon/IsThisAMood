using System.Collections.Generic;
using IsThisAMood.Models;
using IsThisAMood.Models.Database;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IsThisAMood.Services
{
    public interface IParticipantsService
    {
        bool AddEntry(string participantId, Entry entry);
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

        public bool AddEntry(string participantId, Entry entry)
        {
            var builder = Builders<Participant>.Update; 
            var update = builder.Push("Entries", entry);

            var updateResult = _participants.UpdateOne(participant => participant.Id.Equals(participantId), update);

            if(!updateResult.IsAcknowledged) {
                _logger.LogError("Attempting to insert entry {EntryID} was not acknowledged", entry.Id);
                return false;
            }
                
            if(updateResult.ModifiedCount != 1) {
                _logger.LogError("Modified count when attempting to insert entry {EntryID} was {ModifiedCount} ", entry.Id, updateResult.ModifiedCount);
                return false;    
            }
            
            return true;
        }
        
    }
}