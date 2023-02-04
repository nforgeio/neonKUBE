using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Resources.Istio;
using Neon.Kube.Resources.Prometheus;
using Neon.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestOperator
{
    [Rbac<V1ExampleEntity>(RbacVerb.All, Neon.Kube.Resources.EntityScope.Cluster)]
    [Rbac<V1Secret>(RbacVerb.List | RbacVerb.Create, Neon.Kube.Resources.EntityScope.Cluster)]
    [Rbac<V1ServiceMonitor>(RbacVerb.List | RbacVerb.Create, Neon.Kube.Resources.EntityScope.Cluster)]
    [Rbac<V1ServiceAccount>(RbacVerb.List | RbacVerb.Create, Neon.Kube.Resources.EntityScope.Cluster)]
    [Rbac<V1Node>(RbacVerb.Get | RbacVerb.Watch | RbacVerb.List, Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "foo")]
    [Rbac<V1Pod>(RbacVerb.Get | RbacVerb.Watch, Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "default")]
    [Rbac<V1ConfigMap>(RbacVerb.Get | RbacVerb.Watch, Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "default")]
    [Rbac<V1VirtualService>(RbacVerb.Get | RbacVerb.Watch, Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "testaroo")]
    public class ExampleController : IResourceController<V1ExampleEntity>
    {
        private readonly IKubernetes k8s;
        private readonly IFinalizerManager<V1ExampleEntity> finalizerManager;
        private readonly ILogger<ExampleController> logger;

        public ExampleController(
            IKubernetes k8s,
            IFinalizerManager<V1ExampleEntity> finalizerManager,
            ILogger<ExampleController> logger)
        {
            this.k8s = k8s;
            this.finalizerManager = finalizerManager;
            this.logger = logger;
        }

        public async Task<ResourceControllerResult> ReconcileAsync(V1ExampleEntity resource)
        {
            await SyncContext.Clear;

            logger.LogInformation($"RECONCILING: {resource.Name()}");

            await finalizerManager.RegisterAllFinalizersAsync(resource);

            logger.LogInformation($"RECONCILED: {resource.Name()}");

            return ResourceControllerResult.Ok();
        }
    }
}
