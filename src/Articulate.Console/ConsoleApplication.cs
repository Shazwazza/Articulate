using System;
using System.IO;
using Umbraco.Core;

namespace Articulate.Console
{
    public class ConsoleApplication : UmbracoApplicationBase
    {
        private readonly DirectoryInfo _umbracoFolder;

        public ConsoleApplication(DirectoryInfo umbracoFolder)
        {
            _umbracoFolder = umbracoFolder;
        }

        public void StartApplication()
        {
            //Now boot
            GetBootManager()
                .Initialize()
                .Startup(appContext => OnApplicationStarting(this, new EventArgs()))
                .Complete(appContext => OnApplicationStarted(this, new EventArgs()));
        }

        protected override IBootManager GetBootManager()
        {
            return new ConsoleBootManager(this, _umbracoFolder);
        }
    }
}