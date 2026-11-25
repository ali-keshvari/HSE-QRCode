using System.Web.Mvc;

namespace HSEQRCode.Web.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Scan");
        }
    }
}
