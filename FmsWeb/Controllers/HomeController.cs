using System.Web.Mvc;

namespace FmsWeb.Controllers
{
    public class HomeController : Controller
    {

        public ActionResult Index()
        {
            return RedirectToAction("Index", "FMS");

        }

    }
}
