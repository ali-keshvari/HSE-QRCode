using System.Web.Mvc;
using HSEQRCode.Web.Infrastructure;

namespace HSEQRCode.Web.Controllers
{
    public abstract class BaseMvcController : Controller
    {
        private ServiceFactory _services;

        protected ServiceFactory Services => _services ?? (_services = new ServiceFactory());

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _services?.Dispose();
                _services = null;
            }

            base.Dispose(disposing);
        }
    }
}
