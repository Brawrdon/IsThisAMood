using System;
using System.Collections.Generic;
using Alexa.NET.Request;

namespace IsThisAMood.Services {
    public class AlexaSessionStore {
        public readonly Dictionary<string, AlexaSession> Sessions;

        public AlexaSessionStore() {
            Sessions = new Dictionary<string, AlexaSession>();
        }
    }

    public class AlexaSession {
        public readonly Session Session;
        public readonly DateTime Started;
        public DateTime Finshed;

        public AlexaSession(Session session) {
            Session = session;
            Started = DateTime.Now;
        }
    }
}