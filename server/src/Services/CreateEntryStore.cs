using System.Collections.Generic;
using IsThisAMood.Models.Database;

namespace IsThisAMood.Services
{
    public class CreateEntryStore
    {
        public readonly Dictionary<string, Entry> Entries;

        public CreateEntryStore()
        {
            Entries = new Dictionary<string, Entry>();
        }
    }
}