using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using WebPubSub;

namespace Samples
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            //Default route
            RouteTable.Routes.MapRoute("Default", "{action}", new { controller = "Samples", action = "Home" });

            //Start PubSub server
            PubSub.StartServer();
        }

        protected void Application_End()
        {
            //Stop PubSub server
            PubSub.StopServer();
        }
    }
}