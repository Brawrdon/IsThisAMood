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
        public Questionnaire Questionnaire {get; set;}

    }

    public class Questionnaire
    {
        public bool One {get; set;}
        public bool Two {get; set;}
        public bool Three {get; set;}
        public string Four {get; set;}
        public string Five {get; set;}
        public bool Six {get; set;}
        public bool Seven {get; set;}
        public string Eight {get; set;}
        public bool Nine {get; set;}
        public string Ten {get; set;}
        public bool Eleven {get; set;}
        public string Twelve {get; set;}
        public bool Thirteen {get; set;}
        public string Fourteen {get; set;}
        public string Fifteen {get; set;}
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