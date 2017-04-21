using System.Threading.Tasks;
using LAM.BotFramework.Entities;
using LAM.BotFramework.Helpers;
using LAM.BotFramework.ServiceConnectors;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage.Table;

namespace LAM.BotFramework.Code
{
    public static partial class Global
    {
        public static CloudTable TableLog;
        public static CloudTable TableScenario;
        public static AdmAuthentication AdmAuth=null;
        public static bool TranslationEnabled = false;
        public static string ScenarioName = "";
        public static string PragmaOpen = "#!";
        public static string PragmaClose = "!#";
        public static string DebugServicesUrl;

        public static async Task Initialization()
        {
            string storageConnectionString = "LAMBF.StorageConnectionString";
            string scenarioTableName = CloudConfigurationManager.GetSetting("LAMBF.ScenarioTableName");
            string scenarioName = CloudConfigurationManager.GetSetting("LAMBF.ScenarioName");
            string conversationLogTableName = CloudConfigurationManager.GetSetting("LAMBF.LogTableName");
            await Initialization(storageConnectionString, scenarioTableName, scenarioName, conversationLogTableName);
        }
        public static async Task Initialization(string storageConnectionString, string scenarioTableName, string scenarioName, string conversationLogTableName)
        {
            Global.DebugServicesUrl = CloudConfigurationManager.GetSetting("LAMBF.DebugServicesURL");

            ScenarioName = scenarioName;

            CloudTableClient tableClient = CloudStorage.GetTableClient(storageConnectionString);

            // Run these initlializations in parallel
            await ConversationLog.GetTableReference(tableClient, conversationLogTableName).CreateIfNotExistsAsync();
            await Scenario.GetTableReference(tableClient, scenarioTableName).CreateIfNotExistsAsync();

            TableLog = ConversationLog.GetTableReference(tableClient, conversationLogTableName);
            TableScenario = Scenario.GetTableReference(tableClient, scenarioTableName);

            //INIT TRANSLATOR
            string translateClientId = CloudConfigurationManager.GetSetting("LAMBF.TranslateClientId");
            string translateSecret = CloudConfigurationManager.GetSetting("LAMBF.TranslateSecret");
            if (!string.IsNullOrEmpty(translateClientId))
            {
                Global.AdmAuth = new AdmAuthentication(translateClientId, translateSecret);
                Global.TranslationEnabled = true; 
            }

        }
    }
}
