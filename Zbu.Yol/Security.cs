using System;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Web.Security;
using Umbraco.Core;
using Umbraco.Core.Security;

namespace Zbu.Yol
{
    /// <summary>
    /// Provides security management helpers.
    /// </summary>
    internal class Security : IDisposable
    {
        private UmbracoApplicationBase _app;
        private IPrincipal _user;
        private bool _disposed;

        // the only way to retrieve Security is through Impersonate
        private Security()
        { }

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
        public static Security Impersonate(UmbracoApplicationBase app, ApplicationContext context, string login)
        {
            if (app == null)
                throw new ArgumentNullException("app");
            if (context == null)
                throw new ArgumentNullException("context");
            if (string.IsNullOrWhiteSpace(login))
                throw new ArgumentException("Cannot be null nor empty.", "login");

            var security = new Security
            {
                _app = app,
                _user = app.Context.User
            };

            var deployUser = context.Services.UserService.GetByUsername(login);
            if (deployUser == null)
                throw new ArgumentException(string.Format("Invalid logon: \"{0}\".", login));

            // this is ugly, because it is all internal in Umbraco
            // but it works (against 7.1.1)
            // if we don't do it then publishing fails (notification wants a user of some sort...)

            // dunno really if we should fix it so that we don't need to impersonate, or
            // if we should make impersonation much easier so a specific user can be used
            // for running transitions...

            // see: WebSecurity.PerformLogin
            var userDataType = typeof(IBootManager).Assembly.GetType("Umbraco.Core.Security.UserData");
            var userData = Activator.CreateInstance(userDataType);
            var props = userDataType.GetProperties();
            var name = Umbraco.Core.Configuration.UmbracoVersion.Current.Major >= 7 ? "SessionId" : "UserContextId";
            props.Single(x => x.Name == name).SetValue(userData, Guid.NewGuid().ToString("N"));
            props.Single(x => x.Name == "Id").SetValue(userData, deployUser.Id);
            props.Single(x => x.Name == "AllowedApplications").SetValue(userData, deployUser.AllowedSections.ToArray());
            props.Single(x => x.Name == "RealName").SetValue(userData, deployUser.Name);
            props.Single(x => x.Name == "Roles").SetValue(userData, new[] { deployUser.UserType.Alias });
            props.Single(x => x.Name == "StartContentNode").SetValue(userData, deployUser.StartContentId);
            props.Single(x => x.Name == "StartMediaNode").SetValue(userData, deployUser.StartMediaId);
            props.Single(x => x.Name == "Username").SetValue(userData, deployUser.Username);
            props.Single(x => x.Name == "Culture").SetValue(userData, Culture(deployUser.Language));

            // then we don't need userData in the ticket
            // see: UmbracoBackOfficeIdentity.EnsureDeserialized
            app.Context.Items[typeof(UmbracoBackOfficeIdentity)] = userData;

            // see: AuthenticationExtensions.CreateAuthTicketAndCookie
            var ticket = new FormsAuthenticationTicket(
                4,
                deployUser.Name,
                DateTime.Now,
                DateTime.Now.AddMinutes(30),
                false,
                "", //userData,
                "/"
                );

            // see: AuthenticationExtensions.AuthenticateCurrentRequest
            var identity = new UmbracoBackOfficeIdentity(ticket);
            var tempUser = new GenericPrincipal(identity, identity.Roles);
            app.Context.User = tempUser;
            Thread.CurrentPrincipal = tempUser;

            return security;
        }

        // copied over from umbraco.ui because it's internal there ;-(
        private static string Culture(string userLanguage)
        {
            var langFile = global::umbraco.ui.getLanguageFile(userLanguage);
            try
            {
                return langFile.SelectSingleNode("/language").Attributes.GetNamedItem("culture").Value;
            }
            catch
            {
                return string.Empty;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                // see: AuthenticationExtensions.AuthenticateCurrentRequest
                _app.Context.User = _user;
                Thread.CurrentPrincipal = _user;
                _disposed = true;
            }
        }
    }
}
