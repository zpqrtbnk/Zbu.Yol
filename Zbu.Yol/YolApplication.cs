using Umbraco.Core;
using Umbraco.Core.Logging;

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

            UmbracoApplicationBase.ApplicationStarted += (sender, args) =>
            {
                // ensure we have the key-value store
                ZbuKeyValueStore.EnsureInstalled(applicationContext);

                // if some YolManager instances have been initialized then something will happen
                LogHelper.Info<YolApplication>("Run.");
                YolManager.ExecuteInitialized(umbracoApplication, applicationContext);
            };
        }
    }
}
