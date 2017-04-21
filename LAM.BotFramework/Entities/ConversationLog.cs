using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using LAM.BotFramework.Code;

namespace LAM.BotFramework.Entities
{
    /// <summary>
    /// Conversation Log object
    /// Logs all conversation from bot or user
    /// Stores Conversation Log in Azure Table "BOTLOG"
    /// Revised LAM 13.03
    /// </summary>
    public class ConversationLog : BaseEntity
    {
        #region Properties
        public string Text { get; set; }
        public string ConversationID { get; set; }
        public string Origin { get; set; }
        public string Scenario { get; set; }
        public int CurrentQuestion { get; set; }
        #endregion

        private const string TableName = "BotLog";
        public static CloudTable GetTableReference(CloudTableClient client)
        {
            return client.GetTableReference(TableName);
        }
        public override void SetKeys()
        {
            PartitionKey = ConversationID;
            RowKey = DateTime.Now.Ticks.ToString();
        }

        public static IEnumerable<ConversationLog> LoadAll(CloudTable table, string conversationId)
        {
            var query = new TableQuery<ConversationLog>().Where(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, conversationId)
                );
            return table.ExecuteQuery(query);
        }
        public static IEnumerable<ConversationLog> LoadScenario(CloudTable table, string scenario)
        {
            var query = new TableQuery<ConversationLog>().Where(
                    TableQuery.GenerateFilterCondition("Scenario", QueryComparisons.Equal, scenario)
                );
            return table.ExecuteQuery(query);
        }

        public static void Log(Microsoft.Bot.Builder.Dialogs.IDialogContext context, string origin, string message, string scenario, int currentQuestion)
        {
            var replyD = context.MakeMessage();
            LogWithId(replyD.ChannelId, replyD.Conversation.Id, origin, message, scenario, currentQuestion);
        }
        public static void LogWithId(string channelId, string conversationId, string origin, string message, int currentQuestion)
        {
            LogWithId(channelId, conversationId, origin, message, Global.ScenarioName, currentQuestion);
        }
        public static void LogWithId(string channelId, string conversationId, string origin, string message, string scenario, int currentQuestion)
        {
            if (Global.TableLog != null)
            {
                ConversationLog cl = new ConversationLog
                {
                    ConversationID = channelId + "." + conversationId,
                    Origin = origin,
                    Text = message,
                    CurrentQuestion = currentQuestion,
                    Scenario = scenario
                };
                cl.Save(Global.TableLog);
            }
        }
    }
}

