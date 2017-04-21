using LAM.BotFramework.Entities;
using Microsoft.IdentityModel.Protocols;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using LAM.BotFramework.Code;

namespace LAM.BotFramework.ServiceConnectors
{
    public class REST
    {
        /// <summary>
        /// Transfer Full State
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <returns></returns>
        public static async Task<BotProps> Post(string url, BotProps postData)
        {
            Uri U = new Uri(url);
            if (!string.IsNullOrEmpty(Global.DebugServicesUrl ))
            {
                U = new Uri(url.Replace(U.AbsolutePath, Global.DebugServicesUrl));
            }
            string s = U.PathAndQuery;
            HttpClient client = new HttpClient {BaseAddress = U};
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await client.PostAsJsonAsync(U.AbsolutePath, postData);
            response.EnsureSuccessStatusCode();

            BotProps BP = await response.Content.ReadAsAsync<BotProps>();
            return BP;
        }

        /// <summary>
        /// Rest Call, should receive a Dictionary<string,string>
        /// </summary>
        /// <param name="url"></param>
        /// <param name="debugable"></param>
        /// <returns></returns>
        public static async Task<string> Get(string url, bool debugable)
        {
            HttpClient client = new HttpClient();
            if (debugable && !string.IsNullOrEmpty(Global.DebugServicesUrl))
            {
                Uri u = new Uri(url);
                int p = url.IndexOf(u.Host) + u.Host.Length;
                url = Global.DebugServicesUrl + url.Substring(p);
            }
            return await client.GetStringAsync(url);
        }
    }
}