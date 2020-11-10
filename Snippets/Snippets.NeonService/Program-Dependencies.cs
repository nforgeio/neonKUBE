using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Neon.Common;
using Neon.Service;

namespace Service_Dependencies
{
    public static class Program
    {
        public async static Task Main(string[] args)
        {
            // Construct the service and configure it to wait for another
            // become available before calling the services [OnRunAsync()]
            // method.  This is useful for situations where a collection
            // of related services are started at the same time (e.g. via
            // docker-compose or Kubernetes Helm charts giving the service
            // service being relied on a chance to start before this service 
            // tries to access it.
            //
            // This can also be an issue when using Istio/Envoy sidecars
            // in Kubernetes because the Envoy pod sidecar often takes longer
            // to start than the service, meaning that the network will be
            // unavailable for few seconds.
            //
            // We've effectively implemented the retry logic so you don't
            // have to.

            var service = new MyService();

            service.Dependencies.Uris.Add(new Uri("http://waitforthis.com"));

            // You can control how long to wait before a [TimeoutException]
            // will be thrown.  This defaults to 120 seconds.

            service.Dependencies.Timeout = TimeSpan.FromSeconds(30);

            // You can optionally wait longer after the dependencies are ready.

            service.Dependencies.Wait = TimeSpan.FromSeconds(10);

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
            // The http://waitforthis.com endpoint will be ready at this point.

            base.Dispose(disposing);
        }

        protected async override Task<int> OnRunAsync()
        {
            await Task.Delay(TimeSpan.FromDays(365), Terminator.CancellationToken);

            return 0;
        }
    }
}
