using System.Web.Mvc;
using System.Web.Routing;

namespace HSEQRCode.Web
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "ScanLanding",
                url: "scan",
                defaults: new { controller = "Scan", action = "Index" }
            );

            routes.MapRoute(
                name: "ScanByIndicator",
                url: "scan/{indicator}",
                defaults: new { controller = "Scan", action = "ScanByIndicator" }
            );

            routes.MapRoute(
                name: "Lookup",
                url: "lookup",
                defaults: new { controller = "Scan", action = "Lookup" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
