using System;
using System.Web;
using System.Web.Security;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Security;
using Umbraco.Web;
#if UMBRACO_6
using System.Reflection;
using System.Threading;
using System.Security.Principal;
#else
using System.Linq;
using Umbraco.Core.Configuration;
#endif

namespace Zbu.Yol
{
    /// <summary>
    /// Provides security management helpers.
    /// </summary>
    internal class Security : IDisposable
    {
        private HttpCookie _cookie;
        private readonly HttpContextBase _context;
        private bool _disposed;
#if UMBRACO_6
        private readonly UmbracoApplicationBase _app;
        private readonly IPrincipal _user;
#endif

#if UMBRACO_6
        private static string GetCookieName()
        {
            var name = umbraco.UmbracoSettings.GetKey("/settings/security/authCookieName");
            return string.IsNullOrEmpty(name) ? Constants.Web.AuthCookieName : name;
        }

        private static readonly string CookieName = GetCookieName();
#else
        private static readonly string CookieName = UmbracoConfig.For.UmbracoSettings().Security.AuthCookieName;
#endif

        // the only way to retrieve Security is through Impersonate
        private Security(HttpApplication application, ApplicationContext applicationContext, string login)
        {
#if UMBRACO_6
            _app = application as UmbracoApplicationBase;
            if (_app == null)
                throw new ArgumentException("Not an UmbracoApplicationBase.", "application");
            _user = _app.Context.User;
#endif
            _context = new HttpContextWrapper(application.Context);
            BeginImpersonate(applicationContext, login);
        }

        private void BeginImpersonate(ApplicationContext applicationContext, string login)
        {
            // save current cookie (if any)
            // request cookie collection returns null when missing
            _cookie = _context.Request.Cookies[CookieName];

            // ensure login corresponds to an existing user
            var user = applicationContext.Services.UserService.GetByUsername(login);
            if (user == null)
                throw new ArgumentException(string.Format("Invalid logon: \"{0}\".", login));

#if UMBRACO_6
            UmbracoContext.Current.Security.PerformLogin(user.Id);

            var type1 = typeof(Umbraco.Core.Constants).Assembly.GetType("Umbraco.Core.Security.AuthenticationExtensions");
            var meth1 = type1.GetMethod("GetAuthTicket", BindingFlags.Static | BindingFlags.NonPublic);
            var ticket = meth1.Invoke(null, new object[] { _context, CookieName }) as FormsAuthenticationTicket;

            var type2 = typeof(Umbraco.Core.Constants).Assembly.GetType("Umbraco.Core.Security.FormsAuthenticationTicketExtensions");
            var meth2 = type2.GetMethod("CreateUmbracoIdentity", BindingFlags.Static | BindingFlags.Public);
            var identity = meth2.Invoke(null, new object[] { ticket }) as UmbracoBackOfficeIdentity;

            Thread.CurrentThread.CurrentCulture =
                Thread.CurrentThread.CurrentUICulture =
                new System.Globalization.CultureInfo(identity.Culture);
#else
            var currentUser = UmbracoContext.Current.Security.CurrentUser;
            var info = string.Format("Current user: {0}. Impersonating: \"{1}\"",
                currentUser == null ? "<null>" : ("\"" + currentUser.Name + "\""),
                user.Name);
            LogHelper.Info<Security>(info);

            // log the user in
            UmbracoContext.Current.Security.PerformLogin(user);

            // and re-authenticate
            ReAuthenticate();
#endif
        }

        private void EndImpersonate()
        {
            LogHelper.Info<Security>("Restoring current user.");

            // put the original cookie back in place
            // note: after Remove the cookie is actually removed from both Response & Request
            if (_cookie == null)
                _context.Response.Cookies.Remove(CookieName);
            else
                _context.Response.Cookies.Set(_cookie);

#if UMBRACO_6
            _app.Context.User = _user;
            Thread.CurrentPrincipal = _user;
#else
            // and re-authenticate
            ReAuthenticate();
#endif
        }

#if !UMBRACO_6
        private void ReAuthenticate()
        {
            // response cookie collection CREATES a cookie when asked for it!
            var cookie = _context.Response.Cookies.AllKeys.Contains(CookieName)
                ? _context.Response.Cookies[CookieName]
                : null;

            // clear UmbracoBackOfficeIdentity internal cache - should be done in Umbraco
            _context.Items.Remove(typeof(UmbracoBackOfficeIdentity));

            // authenticate
            if (cookie == null) return;
            var ticket = FormsAuthentication.Decrypt(cookie.Value);
            _context.AuthenticateCurrentRequest(ticket, false);
        }
#endif

        /// <summary>
        /// Impersonates an Umbraco user.
        /// </summary>
        /// <param name="app">The Umbraco application.</param>
        /// <param name="context">The current application context.</param>
        /// <param name="login">The login of the user to impersonate.</param>
        /// <returns>A <see cref="Security"/> instance that must be disposed to stop impersonating.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="app"/> is <c>null</c> and/or <paramref name="context"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="login"/> is <c>null</c>, empty, or not a valid user login.</exception>
        /// <exception cref="Exception">What could go wrong?</exception>
        public static Security Impersonate(HttpApplication app, ApplicationContext context, string login)
        {
            if (app == null)
                throw new ArgumentNullException("app");
            if (context == null)
                throw new ArgumentNullException("context");
            if (string.IsNullOrWhiteSpace(login))
                throw new ArgumentException("Cannot be null nor empty.", "login");

            return new Security(app, context, login);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;

            EndImpersonate();
            _disposed = true;
        }
    }
}
