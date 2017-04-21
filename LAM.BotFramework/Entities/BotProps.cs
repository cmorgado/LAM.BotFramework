﻿using Microsoft.Bot.Connector;
using System.Collections.Generic;

namespace LAM.BotFramework.Entities
{
    /// <summary>
    /// Class to share conversation in API calls
    /// Reviewed LAM 13.03
    /// </summary>
    public class BotProps
    {
        //ALL THE PROPERTIES
        public Dictionary<string, string> Properties { get; set; }
        //NEXT QUESTION
        public int NextQuestion { get; set; }
        //RESUMPTION COOKIE
        public ConversationReference ResCookie { get; set; }
        //LIST OF QUESTIONS
        public List<QuestionRow> Scenario { get; set; }
        //OUT RESULT
        public string Result { get; set; }
    }
}
