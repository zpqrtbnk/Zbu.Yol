using System;
using System.Threading;
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

            // cannot run on .ApplicationStarted because it plays with authentication/authorization
            // and therefore wants to have access to the current request's cookie and such
            //umbracoApplication.PostAuthorizeRequest += ExecuteInitialized;
        }

        // ok... the whole mess below

        //protected override void ApplicationInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        //{
        //    base.ApplicationInitialized(umbracoApplication, applicationContext);

        //    // ISSUE - this will add the event to only 1 app
        //    // so the other apps will most likely not like it and crash (I think)
        //    // we need to hook "somewhere" in the pipeline - without Global.asax
        //    umbracoApplication.PostAuthorizeRequest += ExecuteInitialized;
        //}

        //private static void ExecuteInitialized(object sender, EventArgs args)
        //{
        //    var app = sender as UmbracoApplicationBase;

        //    // it should run once and only once - fails
        //    //app.PostAuthorizeRequest -= ExecuteInitialized;

        //    // if some YolManager instances have been initialized then something will happen
        //    LogHelper.Info<YolApplication>("Run.");
        //    //YolManager.ExecuteInitialized(app, ApplicationContext.Current);
        //}

        private static int _ran = 0;

        internal static void RunOnce()
        {
            // sets to 1 and return the previous value - run only if zero
            if (Interlocked.Exchange(ref _ran, 1) > 0) return;

            var app = UmbracoContext.Current.HttpContext.ApplicationInstance;
            LogHelper.Info<YolApplication>("Run.");
            YolManager.ExecuteInitialized(app, ApplicationContext.Current);
        }
    }
}
