ZpqrtBnk Umbraco Deployment  
Copyright (C) Pilotine - ZpqrtBnk 2014  
Distributed under the MIT license  

**DISCLAIMER**
*THIS PROJECT IS CURRENTLY IN PRE-RELEASE STATE AND IS PROVIDED "AS-IS" FOR EVALUATION PURPOSES ONLY*

#### About
An experimental tool that helps deploying Umbraco website. It implements a very basic state machine and
transitions, and will make sure that available transitions run when Umbraco starts, in order to bring
the site to its latest state. A transition is a function that can do about anything, mostly setting up
content types, etc. but there is no limitation to what it can do.

#### Compatibility
It is known to run on top of Umbraco version 7.1.2. It has *not* been tested on other 7.x versions,
and does not seem to build on top of Umbraco version 6.x at the moment, due to a class being internal.
To run on top of 7.x we *already* use internal classes through Reflection (sigh...) but rather than also
do it for 6.x I'd like to figure out whether those internal classes could not be made public.

In order to build, you will need to copy the following DLLs from the Umbraco project into the `lib`
directory: `businesslogic.dll`, `cms.dll`, `interfaces.dll`, `log4net.dll`, `UmbracoCore.dll` and `umbraco.dll`.

#### Security
The transitions execute right after Umbraco has finished initializing, as part of the request that
triggered that initialization. Could be a front-end or a back-end request. Which means that there
probably is no currently logged-in user. At the moment 7.x will just refuse to do some things (create
content...) if no user is logged in (notification service throws), plus it makes sense to have a
special user execute transitions.

In order to achieve this we *impersonate* a user for the time of the execution. This currently is a
bit of a hack but works quite nicely. The goal is to improve the way we do it so it's not a hack
anymore.

WARNING: the manager will run on the first request that initializes Umbraco. That may be a front-end
request. Which means that for a while, during that request, we'll impersonate a back-end user. We
take great care of properly *stopping* impersonation once done, so I tend to think it all *should*
be safe, but I might have missed something obvious and there might be a huge security risk.

#### How does it work
This is how you install the manager in your application:

```c#
public class ConfigureYolManager : ApplicationEventHandler
{
    protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
    {
        base.ApplicationStarted(umbracoApplication, applicationContext);

        // full path name of the file that will contain the state
        var statePath = umbracoApplication.Server.MapPath("~/App_Data/Zbu.Deploy.State");

        // user that will execute the transitions, has to be a real user
        const string impersonate = "demo";

        YolManager.Initialize(statePath, impersonate)
            .DefineTransition(string.Empty, "ae59d4", DoSomething1)
            .DefineTransition("ae59d4", "175f9c", DoSomething2)
            .DefineTransition("175f9c", "36abd8", DoSomething3)
            ;
    }

    private static bool DoSomething1()
    {
        var svcs = ApplicationContext.Current.Services;

        var template = new Template("~/Views/SampleTemplate1.cshtml", "Sample Template 1", "SampleTemplate1")
        {
            Content = Assembly.GetExecutingAssembly().GetManifestResourceText("Zbu.Demos.BuildingTheSite.Views.SampleTemplate1.txt")
        };
        svcs.FileService.SaveTemplate(template);

        return true;
    }

	private static bool DoSomething2()
	{
		...
	}
}
```

You initialize the manager with the full path name of the file that should contain the the state
(somewhere in App_Data), and which user to impersonate while running. Then you define transitions: a
transition has a *start state*, a *target state*, and a function to execute with the transition.

Then the manager will take care of the reset. It will write to the log, at Info level.

It probably is a good idea to maintain the transition functions in other files. However, the entire
transition definitions should be grouped in one place. That way, if two of your developers independently
create a new transition from the final state, they will modify the same bit of code and that will be
picked as a conflict when they merge their work. They will then have to sort things out by creating
proper transition chains.

A few rules:
* there can only be one transition per *start state*
* there should be only one unique final state
* from any transition it should be possible to reach that final state
* there should not be loops

A transitionfunction should return `true` to indicate that it has been successful, else `false`. If a transition
is not successful then the manager will abort and throw. Within a transition, you can access ApplicationContext.Current and
UmbracoContext.Current.

If the manager starts and finds out the site is in a state which is neither the start state of a
transition, nor the target state of a transition (that would be the final state), it aborts and throws.