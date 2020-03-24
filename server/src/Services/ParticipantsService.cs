using System;
using System.Collections.Generic;
using System.Linq;
using Alexa.NET.Request;
using IsThisAMood.Models.Database;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace IsThisAMood.Services
{
    public interface IParticipantsService
    {
        List<Participant> GetParticipants();
        Participant GetParticipant(string email);
        Participant GetParticipantFromToken(string token);
        void AddParticipant(string email, string pin);
        bool AddEntry(string accessToken, string pin, Entry entry);
        bool SetAccessToken(string email, string accessToken, bool alexa);

        List<Entry> GetEntries(string accessToken, string pin, string mood);
        Entry GetEntry(string accessToken, string pin, string name);
        bool DeleteEntry(string accessToken, string name);
        bool CheckPin(string accessToken, string pin);
        void AddEmontionalAwareness(string accessToken, int number, float recognition, float identification, float communication, float context, float decision);
        void AddFeedback(string accessToken, Questionnaire questionnaire);
    }

    public class ParticipantsService : IParticipantsService
    {
        private readonly ILogger<ParticipantsService> _logger;
        private readonly IMongoCollection<Participant> _participants;
        private readonly IParticipantsEncryptionService _encryption;
        
        public ParticipantsService(ILogger<ParticipantsService> logger, IDatabaseSettings settings, IParticipantsEncryptionService encryption)
        {
            _logger = logger;
            _encryption = encryption;
            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);

            _participants = database.GetCollection<Participant>(settings.CollectionName);
        }

        public List<Participant> GetParticipants()
        {
            return _participants.Find(participant => true).ToList();
        }

        public Participant GetParticipant(string email)
        {
            return _participants.Find(participant => participant.Email == email).FirstOrDefault();
        }

        public bool AddEntry(string accessToken, string pin, Entry entry)
        {
            var builder = Builders<Participant>.Update;
            var encryptedEntry = new Entry
            {
                Id = entry.Id,
                Name = _encryption.Encrypt(entry.Name, pin),
                Mood = _encryption.Encrypt(entry.Mood, pin) ,
                Rating = _encryption.Encrypt(entry.Rating, pin),
                Activities = new List<string>()
            };

            foreach (var activity in entry.Activities)
                encryptedEntry.Activities.Add(_encryption.Encrypt(activity, pin));
            
            var update = builder.Push("Entries", encryptedEntry);

            var updateResult = _participants.UpdateOne(participant => participant.AccessToken == accessToken || participant.AlexaAccessToken == accessToken, update);

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

        public bool SetAccessToken(string email, string accessToken, bool alexa)
        {
            var builder = Builders<Participant>.Update;
            var update = builder.Set(participant => participant.AccessToken, accessToken);
;
            if(alexa)
                update = builder.Set(participant => participant.AlexaAccessToken, accessToken);

            var updateResult = _participants.UpdateOne(participant => participant.Email == email, update);

            if (!updateResult.IsAcknowledged)
            {
                _logger.LogError("Attempting to update participant {Email}'s access token was not acknowledged",
                    email);
                return false;
            }

            if (updateResult.ModifiedCount != 1)
            {
                _logger.LogError(
                    "Modified count when attempting to update access token for participant {Email} was {ModifiedCount}",
                    email, updateResult.ModifiedCount);
                return false;
            }

            _logger.LogDebug("Participant {Email}'s access token was updated", email);
            
            return true;
        }

        public List<Entry> GetEntries(string accessToken, string pin, string mood = null)
        {
            var participant = GetParticipantFromToken(accessToken);
            
            var entries = participant.Entries;

            if (entries.Count == 0)
                return entries;

            if(mood != null)
                entries = entries.FindAll(x => x.Mood == mood);

            entries.Reverse();

            var decryptedEntries = new List<Entry>();

            foreach(var entry in entries)
                decryptedEntries.Add(DecryptEntry(entry, pin));

            return decryptedEntries;
        }

        public Participant GetParticipantFromToken(string accessToken)
        {
            var participant = _participants.Find(participant => participant.AccessToken == accessToken || participant.AlexaAccessToken == accessToken).FirstOrDefault();
            if (participant == null)
                throw new Exception("Participant doesn't exist");

            return participant;
        }

        public Entry GetEntry(string accessToken, string pin, string name)
        {
            var entries = GetEntries(accessToken, pin);
            return entries.Select(x => x).FirstOrDefault(x => x.Name == name);
        }

        public bool DeleteEntry(string accessToken, string name)
        {
            var builder = Builders<Participant>.Update;
            var update = builder.PullFilter("Entries", Builders<Entry>.Filter.Eq("Name", name));

            var updateResult = _participants.UpdateOne(participant => participant.AccessToken == accessToken, update);

             if (!updateResult.IsAcknowledged)
            {
                _logger.LogError("Attempting to remove entry {EntryName} was not acknowledged", name);
                return false;
            }

            if (updateResult.ModifiedCount != 1)
            {
                _logger.LogError("Modified count when attempting to removing entry {EntryName} was {ModifiedCount}",
                    name, updateResult.ModifiedCount);
                return false;
            }

            _logger.LogDebug("Entry {EntryName} was removed", name);

            return true;
        }

        public bool CheckPin(string accessToken, string pin)
        {
            var participant = GetParticipantFromToken(accessToken);

            return participant.Pin == pin;
        }

        private Entry DecryptEntry(Entry entry, string pin)
        {
            var decryptedEntry = new Entry
            {
                Id = entry.Id,
                Name = _encryption.Decrypt(entry.Name, pin).Replace("\0", string.Empty),
                Mood = _encryption.Decrypt(entry.Mood, pin).Replace("\0", string.Empty),
                Rating = _encryption.Decrypt(entry.Rating, pin).Replace("\0", string.Empty),
                Activities = new List<string>()
            };
            
            foreach (var activity in entry.Activities)
                decryptedEntry.Activities.Add(_encryption.Decrypt(activity, pin).Replace("\0", string.Empty));

            return decryptedEntry;
            
        }

        public void AddParticipant(string email, string pin)
        {
            var participant = new Participant 
            {
                Email = email,
                Pin = pin,
                Entries = new List<Entry>(),
                AccessToken = "",
                AlexaAccessToken = "",
                PreTest = new EmotionalAwareness 
                {
                    Communication = 0,
                    Contextualisation = 0,
                    Identification = 0,
                    Decision = 0,
                    Recognition = 0
                },
                PostTest = new EmotionalAwareness 
                {
                    Communication = 0,
                    Contextualisation = 0,
                    Identification = 0,
                    Decision = 0,
                    Recognition = 0
                },
                Questionnaire = new Questionnaire 
                {
                    One = false,
                    Two = false,
                    Three = false,
                    Four = "",
                    Five = "",
                    Six = false,
                    Seven = false,
                    Eight = "",
                    Nine = false,
                    Ten = "",
                    Eleven = false,
                    Twelve = "",
                    Thirteen = false,
                    Fourteen = "",
                    Fifteen = ""
                }
            };

            _participants.InsertOne(participant);
        }

        public void AddEmontionalAwareness(string accessToken, int number, float recognition, float identification, float communication, float context, float decision)
        {

            var builder = Builders<Participant>.Update;
            var update = builder
                .Set(participant => participant.PreTest.Recognition, recognition)
                .Set(participant => participant.PreTest.Identification, identification)
                .Set(participant => participant.PreTest.Communication, communication)
                .Set(participant => participant.PreTest.Contextualisation, context)
                .Set(participant => participant.PreTest.Decision, decision);

            if(number == 2) 
            {
                update = builder
                .Set(participant => participant.PostTest.Recognition, recognition)
                .Set(participant => participant.PostTest.Identification, identification)
                .Set(participant => participant.PostTest.Communication, communication)
                .Set(participant => participant.PostTest.Contextualisation, context)
                .Set(participant => participant.PostTest.Decision, decision);
            }
            
            var updateResult = _participants.UpdateOne(participant => participant.AccessToken == accessToken, update);
            
            if (!updateResult.IsAcknowledged)
            {
                _logger.LogError("Attempting to update participant emotions not acknowledged");
            }

            if (updateResult.ModifiedCount != 1)
            {
                _logger.LogError(
                    "Modified count when attempting to update emotions was {ModifiedCount}", updateResult.ModifiedCount);
            }

            _logger.LogDebug("Participant emotions was updated");
        }

        public void AddFeedback(string accessToken, Questionnaire questionnaire)
        {
            var builder = Builders<Participant>.Update;
            var update = builder.Set(participant => participant.Questionnaire, questionnaire)
                        .Set(participant => participant.AccessToken, "");

            var updateResult = _participants.UpdateOne(participant => participant.AccessToken == accessToken, update);
            
            if (!updateResult.IsAcknowledged)
            {
                _logger.LogError("Attempting to update feedback questionnaire not acknowledged");
            }

            if (updateResult.ModifiedCount != 1)
            {
                _logger.LogError(
                    "Modified count when attempting to update feedback was {ModifiedCount}", updateResult.ModifiedCount);
            }

            _logger.LogDebug("Participant feedback was updated");

        }
    }
}