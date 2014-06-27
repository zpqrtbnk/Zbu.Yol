using System;
using System.Web;
using Zbu.Yol;

[assembly: PreApplicationStartMethod(typeof(YolModule), "Install")]

namespace Zbu.Yol
{
    public class YolModule : IHttpModule
    {
        public void Init(HttpApplication app)
        {
            // need to take place after BeginRequest and before PostResolveRequestCache
            // which is where Umbraco will actually process the request (prepare, etc)
            app.PostAuthenticateRequest += PostAuthenticateRequest;
        }

        private static void PostAuthenticateRequest(object sender, EventArgs e)
        {
            //var app = (HttpApplication) sender;
            YolApplication.RunOnce();
        }

        public void Dispose()
        {
            // nothing
        }

        public static void Install()
        {
            HttpApplication.RegisterModule(typeof(YolModule));
        }
    }
}
