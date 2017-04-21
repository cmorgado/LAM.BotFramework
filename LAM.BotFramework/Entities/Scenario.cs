using Microsoft.WindowsAzure.Storage.Table;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LAM.BotFramework.Code;

namespace LAM.BotFramework.Entities
{
    public class Scenario : TableEntity
    {
        #region PROPERTIES
        public string Version { get; set; }
        public string Definition { get; set; }
        #endregion

        public static CloudTable GetTableReference(CloudTableClient client, string tableName)
        {
            return client.GetTableReference(tableName);
        }

        public virtual void Save(string name)
        {
            PartitionKey = name;
            RowKey = Version;
            TableOperation insertOperation = TableOperation.InsertOrReplace(this);
            Global.TableScenario.Execute(insertOperation);
        }
        public Task SaveAsync(string name)
        {
            PartitionKey = name;
            RowKey = Version;
            TableOperation insertOperation = TableOperation.InsertOrReplace(this);
            return Global.TableScenario.ExecuteAsync(insertOperation);
        }
        public static string LoadRecentScenario(string name)
        {
            var query = new TableQuery<Scenario>().Where(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, name)
                );
            IEnumerable<Scenario> icl = Global.TableScenario.ExecuteQuery(query);
            if (icl.Any())
                return icl.First().Definition;
            else
                return "";
        }
    }
}
