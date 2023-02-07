using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using k8s;
using k8s.Models;

using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Resources;
using Neon.Tasks;

namespace TestOperator
{
    /// <summary>
    /// Example controller
    /// </summary>
    [Controller(AutoRegisterFinalizers = true, ManageCustomResourceDefinitions = true)]
    [RbacRule<V1ExampleEntity>(RbacVerb.All, EntityScope.Cluster)]
    public class ExampleController : IResourceController<V1ExampleEntity>
    {
        private readonly IKubernetes k8s;
        private readonly IFinalizerManager<V1ExampleEntity> finalizerManager;
        private readonly ILogger<ExampleController> logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s"></param>
        /// <param name="finalizerManager"></param>
        /// <param name="logger"></param>
        public ExampleController(
            IKubernetes k8s,
            IFinalizerManager<V1ExampleEntity> finalizerManager,
            ILogger<ExampleController> logger)
        {
            this.k8s = k8s;
            this.finalizerManager = finalizerManager;
            this.logger = logger;
        }

        /// <summary>
        /// Reconciles.
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        public async Task<ResourceControllerResult> ReconcileAsync(V1ExampleEntity resource)
        {
            await SyncContext.Clear;

            logger.LogInformation($"RECONCILING: {resource.Namespace()}/{resource.Name()}");

            await finalizerManager.RegisterAllFinalizersAsync(resource);

            logger.LogInformation($"RECONCILED: {resource.Namespace()}/{resource.Name()}");

            return ResourceControllerResult.Ok();
        }

        /// <summary>
        /// Deleted.
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        public async Task DeletedAsync(V1ExampleEntity resource)
        {
            logger.LogInformation($"DELETED: {resource.Name()}");
        }
    }
}
