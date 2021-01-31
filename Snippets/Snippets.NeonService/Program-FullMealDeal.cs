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
        public static Counter runTimeCounter = Metrics.CreateCounter("run-time", "Service run time in seconds.");

        public MyService() : base("my-service")
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        protected async override Task<int> OnRunAsync()
        {
            while (!Terminator.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                runTimeCounter.Inc();
            }

            return 0;
        }
    }
}
