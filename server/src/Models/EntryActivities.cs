using System.Collections.Generic;
using IsThisAMood.Models.Database;

namespace IsThisAMood.Models
{
    public class EntryActivities
    {
        public List<string> Activities { get; }
        public Entry Entry { get; }

        public EntryActivities(Entry entry)
        {
            Activities = new List<string>();
            Entry = entry;
        }
    }
}