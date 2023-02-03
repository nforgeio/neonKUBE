using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Kube.Operator.Finalizer;
using Neon.Tasks;

using k8s;
using k8s.Models;

namespace OperatorTemplate
{
    public class ExampleFinalizer : IResourceFinalizer<V1ExampleEntity>
    {
        private readonly IKubernetes k8s;
        private readonly ILogger<ExampleController> logger;
        public ExampleFinalizer(
            IKubernetes k8s,
            ILogger<ExampleController> logger)
        {
            this.k8s = k8s;
            this.logger = logger;
        }

        public async Task FinalizeAsync(V1ExampleEntity resource)
        {
            await SyncContext.Clear;

            logger.LogInformation($"FINALIZED: {resource.Name()}");
        }
    }
}
