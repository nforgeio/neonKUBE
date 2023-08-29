using System;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Neon.Operator.Finalizers;
using Neon.Tasks;

namespace TestOperator
{
    public class ExampleFinalizer : ResourceFinalizerBase<V1ExampleEntity>
    {
        private readonly IKubernetes k8s;
        private readonly ILogger<ExampleV1Controller> logger;
        public ExampleFinalizer(
            IKubernetes k8s,
            ILogger<ExampleV1Controller> logger)
        {
            this.k8s = k8s;
            this.logger = logger;
        }

        public async Task FinalizeAsync(V1ExampleEntity resource)
        {
            await SyncContext.Clear;

            logger.LogInformation($"FINALIZING: {resource.Name()}");

            await Task.Delay(1000);

            logger.LogInformation($"FINALIZED: {resource.Name()}");
        }
    }
}
