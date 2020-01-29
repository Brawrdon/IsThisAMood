using System;
using System.Collections.Generic; 

namespace IsThisAMood.Services {
    public class SessionStore {
        public readonly Dictionary<string, Session> Sessions;

        public SessionStore() {
            Sessions = new Dictionary<string, Session>();
        }
    }

    public class Session {
        public readonly Alexa.NET.Request.Session RequestSession;
        public readonly DateTime Started;
        public DateTime Finshed;

        public Session(Alexa.NET.Request.Session session) {
            RequestSession = session;
            Started = DateTime.Now;
        }
    }
}