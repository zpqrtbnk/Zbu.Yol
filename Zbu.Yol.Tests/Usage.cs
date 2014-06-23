using System.Reflection;
using Umbraco.Core;
using Umbraco.Core.Models;

namespace Zbu.Yol.Tests
{
    class Usage : ApplicationEventHandler
    {
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarted(umbracoApplication, applicationContext);

            // use the default manager, and get login from config
            YolManager.CreateDefault()
                .DefineTransition(string.Empty, "ae59d4", DoSomething)
                ;
        }

        private static bool DoSomething()
        {
            var svcs = ApplicationContext.Current.Services;

            var template = new Template("~/Views/SampleTemplate1.cshtml", "Sample Template 1", "SampleTemplate1")
            {
                Content = Assembly.GetExecutingAssembly().GetManifestResourceText("Zbu.Demos.BuildingTheSite.Views.SampleTemplate1.txt")
            };
            svcs.FileService.SaveTemplate(template);

            return true;
        }
    }
}
