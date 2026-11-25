using System;
using System.Security.Principal;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;

namespace HSEQRCode.Web
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            HttpCookie authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrEmpty(authCookie.Value))
            {
                return;
            }

            try
            {
                FormsAuthenticationTicket ticket = FormsAuthentication.Decrypt(authCookie.Value);
                if (ticket == null || ticket.Expired)
                {
                    return;
                }

                string[] roles = string.IsNullOrWhiteSpace(ticket.UserData)
                    ? new string[0]
                    : ticket.UserData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                var identity = new FormsIdentity(ticket);
                HttpContext.Current.User = new GenericPrincipal(identity, roles);
            }
            catch
            {
                // Ignore invalid/tampered auth cookie
            }
        }
    }
}
