using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IsThisAMood.Models.Database
{
    public class Entry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Name { get; set; }

        public string Mood { get; set; }

        public string Rating { get; set; }

        public List<string> Activities { get; set; }
    }
}