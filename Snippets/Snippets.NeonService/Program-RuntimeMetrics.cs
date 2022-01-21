using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Service;

using Prometheus;               // From the [prometheus-net] nuget package
using Prometheus.DotNetRuntime; // From the [prometheus-net.DotNetRuntime] nuget package

namespace Service_RuntimeMetrics
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var service = new MyService();

            service.MetricsOptions.Mode = MetricsMode.Scrape;

            // For .NET Core 2.2+ based services, we highly recommend that you enable
            // collection of the .NET Runtime metrics as well to capture information
            // about threads, memory, exceptions, JIT statistics, etc.  You can do this
            // with just one more statement:

            service.MetricsOptions.GetCollector = () => 
                DotNetRuntimeStatsBuilder
                    .Default()
                    .StartCollecting();

            // The line above collects all of the available runtime metrics.  You can 
            // customize which metrics are collected using this commented line, but
            // we recommend collecting everything because you never know what you'll
            // need:

            //service.MetricsOptions.GetCollector = () =>
            //    DotNetRuntimeStatsBuilder
            //        .Customize()
            //        .WithContentionStats()
            //        .WithJitStats()
            //        .WithThreadPoolStats()
            //        .WithGcStats()
            //        .WithExceptionStats()
            //        .StartCollecting();

            await new MyService().RunAsync();
        }
    }

    public class MyService : NeonService
    {
        private static Counter myCounter;

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
