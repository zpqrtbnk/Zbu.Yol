using System.Reflection;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Models;

namespace Zbu.Yol.Tests
{
    class Usage : ApplicationEventHandler
    {
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarted(umbracoApplication, applicationContext);

            // full path name of the file that will contain the state
            var statePath = umbracoApplication.Server.MapPath("~/App_Data/Zbu.Deploy.State");

            // user that will execute the transitions, has to be a real user
            const string impersonate = "jdemo";

            YolManager.Initialize(statePath, impersonate)
                .DefineTransition(string.Empty, "ae59d4", DoSomething)
                ;
        }

        private static bool DoSomething(ApplicationContext applicationContext, HttpServerUtility server)
        {
            var svcs = applicationContext.Services;

            var template = new Template("~/Views/SampleTemplate1.cshtml", "Sample Template 1", "SampleTemplate1")
            {
                Content = Assembly.GetExecutingAssembly().GetManifestResourceText("Zbu.Demos.BuildingTheSite.Views.SampleTemplate1.txt")
            };
            svcs.FileService.SaveTemplate(template);

            return true;
        }
    }
}
