using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IsThisAMood.Models.Database
{
    public class Participant
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Email { get; set; }
        public string Pin { get; set; }
        public List<Entry> Entries { get; set; }
        public string AccessToken { get; set; }
        public string AlexaAccessToken {get; set;}

        public EmotionalAwareness PreTest {get; set;}
                
        public EmotionalAwareness PostTest {get; set;}

 
    }

    public class EmotionalAwareness
    {
        public float Communication {get; set;}
        public float Contextualisation {get; set;}
        public float Decision {get; set;}
        public float Identification {get; set;}
        public float Recognition {get; set;}
    }
}