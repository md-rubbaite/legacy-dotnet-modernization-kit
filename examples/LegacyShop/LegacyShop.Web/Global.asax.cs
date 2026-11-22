using System.Web.Mvc;
using System.Web.Routing;

namespace LegacyShop.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            RouteTable.Routes.MapRoute("Default", "{controller}/{action}/{id}");
        }
    }
}
