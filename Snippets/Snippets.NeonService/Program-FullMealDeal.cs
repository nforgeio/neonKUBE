#pragma warning disable CS8892 // Method 'Program.Main(string[])' will not be used as an entry point because a synchronous entry point 'Program.Main(string[])' was found.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Service;

using Prometheus;               // From the [prometheus-net] nuget package
using Prometheus.DotNetRuntime; // From the [prometheus-net.DotNetRuntime] nuget package

namespace Service_FullMealDeal
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var service = new MyService();

            service.Dependencies.Uris.Add(new Uri("http://waitforthis.com"));

            service.MetricsOptions.Mode = MetricsMode.Scrape;
            service.MetricsOptions.GetCollector = () => 
                DotNetRuntimeStatsBuilder
                    .Default()
                    .StartCollecting();

            await new MyService().RunAsync();
        }
    }

    public class MyService : NeonService
    {
        private readonly Counter myCounter;

        public MyService() : base("my-service")
        {
            // [NeonService.MetricsPrefix]:
            //
            // The property returns a metrics name prefix based on the service name by default
            // or a custom prefix passed as a NeonService cnstructor parameter.  As a convention,
            // you should use this to prefix all of your metrics counters.

            myCounter = Metrics.CreateCounter($"{MetricsPrefix}mycounter", "Counter that increments once a second.");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        protected async override Task<int> OnRunAsync()
        {
            while (!Terminator.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                myCounter.Inc();
            }

            return 0;
        }
    }
}
