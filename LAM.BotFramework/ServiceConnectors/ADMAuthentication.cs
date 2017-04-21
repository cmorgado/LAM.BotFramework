using System;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Web;
using System.IO;

namespace LAM.BotFramework.ServiceConnectors
{
    /// <summary>
    /// AdmAuthentication for token 
    /// Revised LAM 13.03
    /// </summary>

    [DataContract]
    public class AdmAccessToken
    {
        [DataMember]
        public string access_token { get; set; }
        [DataMember]
        public string token_type { get; set; }
        [DataMember]
        public string expires_in { get; set; }
        [DataMember]
        public string scope { get; set; }
    }
    public class AdmAuthentication
    {
        public static readonly string DatamarketAccessUri = "https://datamarket.accesscontrol.windows.net/v2/OAuth2-13";
        private string clientId="";
        private string clientSecret = "";
        private string request;
        private AdmAccessToken token;
        private Timer accessTokenRenewer;
        //Access token expires every 10 minutes. Renew it every 9 minutes only.
        private const int RefreshTokenDuration = 9;
        public AdmAuthentication(string clientId, string clientSecret)
        {
            this.clientId = clientId;
            this.clientSecret = clientSecret;
            if (string.IsNullOrEmpty(clientId)) {
                request = "";
                token = null;
            }
            else
            {
                //If clientid or client secret has special characters, encode before sending request
                request =
                    $"grant_type=client_credentials&client_id={HttpUtility.UrlEncode(clientId)}&client_secret={HttpUtility.UrlEncode(clientSecret)}&scope=http://api.microsofttranslator.com";
                token = HttpPost(DatamarketAccessUri, request);
                //renew the token every specfied minutes
                accessTokenRenewer = new Timer(new TimerCallback(OnTokenExpiredCallback), this, TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
            }
        }
        public AdmAccessToken GetAccessToken()
        {
            return token;
        }
        private void RenewAccessToken()
        {
            AdmAccessToken newAccessToken = HttpPost(DatamarketAccessUri, request);
            //swap the new token with old one
            //Note: the swap is thread unsafe
            token = newAccessToken;
            Console.WriteLine($"Renewed token for user: {clientId} is: {token.access_token}");
        }
        private void OnTokenExpiredCallback(object stateInfo)
        {
            try
            {
                RenewAccessToken();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed renewing access token. Details: {ex.Message}");
            }
            finally
            {
                try
                {
                    accessTokenRenewer.Change(TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to reschedule the timer to renew access token. Details: {ex.Message}");
                }
            }
        }
        private AdmAccessToken HttpPost(string datamarketAccessUri, string requestDetails)
        {
            //Prepare OAuth request 
            WebRequest webRequest = WebRequest.Create(datamarketAccessUri);
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST";
            byte[] bytes = Encoding.ASCII.GetBytes(requestDetails);
            webRequest.ContentLength = bytes.Length;
            using (Stream outputStream = webRequest.GetRequestStream())
            {
                outputStream.Write(bytes, 0, bytes.Length);
            }
            using (WebResponse webResponse = webRequest.GetResponse())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AdmAccessToken));
                //Get deserialized object from JSON stream
                AdmAccessToken token = (AdmAccessToken)serializer.ReadObject(webResponse.GetResponseStream());
                return token;
            }
        }
    }

}
