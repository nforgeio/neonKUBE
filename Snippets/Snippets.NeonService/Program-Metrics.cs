using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Service;

using Prometheus;       // From the [prometheus-net] nuget package

namespace Service_Metrics
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // You can enable Prometheus metrics with just one line of code!
            //
            // This enables scraping mode where the service is configured as an
            // exporter that Prometheus can scrape periodically on port [9762],
            // the standard NeonService metrics port.
            //
            // You can customize this port using the [MetricsOptions.Port]
            // property to avoid port conflicts by we recommend standarizing 
            // on the default port when running in a container where port
            // conflicts won't be an issue.
            //
            // You can also configure the service to push metrics to a
            // Prometheus Pushgateway but doing this should be limited
            // to special situations.  See the Prometheus documentation 
            // for more information.

            var service = new MyService();

            service.MetricsOptions.Mode = MetricsMode.Scrape;

            await new MyService().RunAsync();
        }
    }

    public class MyService : NeonService
    {
        // Define a custom Prometheus counter.  This value will be able to be
        // tracked on Prometheus related dashboards, alert rules, etc.

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
            // We're just increment the runtime counter once a second until
            // see see the termination signal.

            while (!Terminator.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                myCounter.Inc();
            }

            return 0;
        }
    }
}
