using System;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Web.ViewModels;

namespace HSEQRCode.Web.Controllers
{
    public class AccountController : BaseMvcController
    {
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToSafeUrl(returnUrl);
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model)
        {
            if (model == null)
            {
                return View(new LoginViewModel());
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            AppUser user = await Services.AuthService.ValidateUserAsync(model.Username, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور اشتباه است.");
                return View(model);
            }

            string roles = user.IsAdmin ? "Admin,Inspector" : "Inspector";
            var ticket = new FormsAuthenticationTicket(
                1,
                user.Username,
                DateTime.Now,
                DateTime.Now.AddMinutes(model.RememberMe ? 43200 : 480),
                model.RememberMe,
                roles,
                FormsAuthentication.FormsCookiePath);

            string encrypted = FormsAuthentication.Encrypt(ticket);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted)
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath
            };

            if (model.RememberMe)
            {
                cookie.Expires = ticket.Expiration;
            }

            Response.Cookies.Add(cookie);
            return RedirectToSafeUrl(model.ReturnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Scan");
        }

        private ActionResult RedirectToSafeUrl(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Scan");
        }
    }
}
