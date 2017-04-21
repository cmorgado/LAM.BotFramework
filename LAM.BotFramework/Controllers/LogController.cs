using LAM.BotFramework.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using LAM.BotFramework.Code;
using LAM.BotFramework.Models;

namespace LAM.BotFramework.Controllers
{
    /// <summary>
    /// LOG Scenario Statistics
    /// REVISED LAM 13.03
    /// </summary>
    public class LogApiController : ApiController
    {
        /// <summary>
        /// Retrieves the statistics for the Scenario, beware it's memory intensive
        /// </summary>
        /// <param name="scenario"></param>
        /// <param name="dateStart"></param>
        /// <param name="dateFinish"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("api/LAM/LoadScenario")]
        public List<KeyValuePair<int, int>> LoadScenario(string scenario, string dateStart, string dateFinish)
        {
            IEnumerable<ConversationLog> cl = ConversationLog.LoadScenario(Global.TableLog, scenario);

            int[] a = new int[500];
            foreach (ConversationLog item in cl)
            {
                if ((item.Origin == "USER") && (item.CurrentQuestion > -1))
                    a[item.CurrentQuestion]++;
            }
            List<KeyValuePair<int, int>> lii = new List<KeyValuePair<int, int>>();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != 0)
                    lii.Add(new KeyValuePair<int, int>(i, a[i]));
            }
            return lii;
        }

        /// <summary>
        /// Returns the most recent Scenario definition
        /// </summary>
    /// <param name="scenarioName"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("api/LAM/LoadDefinition")]
        public string LoadDefinition(string scenarioName)
        {
            return Scenario.LoadRecentScenario(scenarioName);
        }

        /// <summary>
        /// Saves the Scenario
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("api/LAM/SaveDefinition")]
        public string SaveDefinition([FromBody]PostScenario json)
        {
            if (json != null)
            {
                Scenario s = new Scenario
                {
                    Definition = json.Definition,
                    Version = json.Version
                };

                Task T = s.SaveAsync(json.Scenario);
            }
            return "";
        }
    }
}
