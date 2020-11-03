using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Neon.Common;
using Neon.Service;

namespace Service_Basic
{
    public static class Program
    {
        public async static Task Main(string[] args)
        {
            // Launch the service.

            await new MyService().RunAsync();
        }
    }

    public class MyService : NeonService
    {
        public MyService() : base("my-service")
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            // This is where you should dispose thing like your webapp, 
            // database connections, etc.

            base.Dispose(disposing);
        }

        protected async override Task<int> OnRunAsync()
        {
            // You can retrieve configuration settings from environment variables
            // or files passed by Kubernetes, Docker, or unit tests via these
            // base clase methods:

            var mySetting    = GetEnvironmentVariable("MY_SETTING");
            var myConfigPath = GetConfigFilePath("/my-config.yaml");

            // Use this base class property to log things.  These will be picked up
            // automatically by Kubernetes and Docker.

            Log.LogInfo("HELLO WORLD!");

            // This is where your service does its thing: like starting a webapp,
            // process data from queues, implementing a database, or whatever.
            // Note that there's no need to wrap this code with a try...catch
            // to log exceptions because the base class already does that for you.
            //
            // We're just going to do pretend by doing nothing here except for 
            // wait for a termination signal from Kubernetes, Docker, or the unit
            // test framework.

            await Task.Delay(TimeSpan.FromDays(365), Terminator.CancellationToken);

            // Return a non-zero exit code when the service terminates normally.

            return 0;
        }
    }
}
