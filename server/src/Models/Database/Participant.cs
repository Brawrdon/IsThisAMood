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
        public int Communication {get; set;}
        public int Contextualisation {get; set;}
        public int Decision {get; set;}
        public int Identification {get; set;}
        public int Recognition {get; set;}

        public int CommunicationTwo {get; set;}
        public int ContextualisationTwo {get; set;}
        public int DecisionTwo {get; set;}
        public int IdentificationTwo {get; set;}
        public int RecognitionTwo {get; set;}
    }
}