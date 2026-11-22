using System.Web;
using System.Web.Mvc;

namespace LegacyShop.Web.Controllers
{
    public class OrdersController : Controller
    {
        public ActionResult Index()
        {
            var user = HttpContext.Current.User.Identity.Name;
            return View((object)user);
        }
    }
}
