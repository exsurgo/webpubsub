using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace Samples.Controllers
{
    public class SamplesController : Controller
    {
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string identity, string name)
        {
            //Create authentication cookie 
            HttpCookie authCookie = FormsAuthentication.GetAuthCookie(identity, false);

            //Important: Needed for flash websocket fallback
            authCookie.HttpOnly = false;

            //Create authentication ticket
            FormsAuthenticationTicket ticket = FormsAuthentication.Decrypt(authCookie.Value);

            //Store name in user data
            FormsAuthenticationTicket newTicket = new FormsAuthenticationTicket(ticket.Version, ticket.Name, ticket.IssueDate, ticket.Expiration, ticket.IsPersistent, name);

            //Encrypt and add to response
            authCookie.Value = FormsAuthentication.Encrypt(newTicket);
            Response.Cookies.Add(authCookie);

            //Redirect home
            return Redirect("/");
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return Redirect("/");
        }

        public ActionResult Home()
        {
            return View();
        }

        public ActionResult Chat()
        {
            return View();
        }

        public ActionResult Concurrency()
        {
            return View();
        }
    }
}
