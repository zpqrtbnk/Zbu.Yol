using System;
using System.Threading;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web;

namespace Zbu.Yol
{
    /// <summary>
    /// Registers the <see cref="YolManager"/> with Umbraco's infrastructure.
    /// </summary>
    public class YolApplication : ApplicationEventHandler
    {
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarted(umbracoApplication, applicationContext);

            LogHelper.Info<YolApplication>("Configure.");

            // ensure we have the key-value store
            UmbracoApplicationBase.ApplicationStarted += (sender, args) => ZbuKeyValueStore.EnsureInstalled(applicationContext);

            // - cannot run on .ApplicationStarted because it plays with authentication/authorization
            // and therefore wants to have access to the current request's cookie and such
            // - cannot register it here either because it would add it to only one HttpApplication
            // and therefore cause inconsitencies - and a crypting exception in ASP.NET pipeline
            // - so in order to properly register it... have to use a module.

            //umbracoApplication.PostAuthorizeRequest += ExecuteInitialized;
        }

        private static int _ran = 0;

        internal static void RunOnce()
        {
            // this code will execute once per request
            // we make sure that only one request will run the upgrade
            // we do not block the other requests while that one is running
            // and if the upgrade fails (throws)... have to restart the app

            // if for any reason the context is null, maybe that's a client-side
            // request (for an image, javascript, whatever) but anyway we cannot
            // do anything and have to wait for a proper request.
            if (UmbracoContext.Current == null)
                return;

            // sets to 1 and return the previous value - run only if zero
            if (Interlocked.Exchange(ref _ran, 1) > 0) return;

            var app = UmbracoContext.Current.HttpContext.ApplicationInstance;
            LogHelper.Info<YolApplication>("Run (once).");
            YolManager.ExecuteInitialized(app, ApplicationContext.Current);
            LogHelper.Info<YolApplication>("Done running (once).");
        }
    }
}
