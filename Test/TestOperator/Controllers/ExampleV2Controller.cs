using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using k8s;
using k8s.Models;

using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Finalizers;
using Neon.Operator.Rbac;
using Neon.Operator.ResourceManager;
using Neon.Tasks;

namespace TestOperator
{
    /// <summary>
    /// Example controller
    /// </summary>
    [RbacRule<V2ExampleEntity>(Verbs = RbacVerb.All)]
    [RbacRule<V1ExampleClusterEntity>(Verbs = RbacVerb.All)]
    [DependentResource<V1ExampleClusterEntity>]
    public class ExampleV2Controller : ResourceControllerBase<V2ExampleEntity>
    {
        private readonly IKubernetes k8s;
        private readonly IFinalizerManager<V2ExampleEntity> finalizerManager;
        private readonly ILogger<ExampleV2Controller> logger;

        public ExampleV2Controller(
            IKubernetes k8s,
            IFinalizerManager<V2ExampleEntity> finalizerManager,
            ILogger<ExampleV2Controller> logger)
        {
            this.k8s = k8s;
            this.finalizerManager = finalizerManager;
            this.logger = logger;
        }

        public async Task<ResourceControllerResult> ReconcileAsync(V2ExampleEntity resource)
        {
            await SyncContext.Clear;

            logger.LogInformation($"RECONCILED: {resource.Namespace()}/{resource.Name()}");

            return ResourceControllerResult.Ok();
        }

        public async Task DeletedAsync(V2ExampleEntity resource)
        {
            logger.LogInformation($"DELETED: {resource.Name()}");
        }
    }
}
