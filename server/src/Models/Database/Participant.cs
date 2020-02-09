using System;
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
        
        public string Username { get; set; }
        
        public string Password { get; set; }
                
        public List<Entry> Entries { get; set; } 
        
        public bool IsActive { get; set; }
    }
}