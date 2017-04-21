using System.Threading.Tasks;
using System.Web.Http;
using LAM.BotFramework.Code;

namespace DemoBot
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            #region LAM.BotFramework
            Task T = Global.Initialization();
            T.Wait();
            #endregion

            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}
