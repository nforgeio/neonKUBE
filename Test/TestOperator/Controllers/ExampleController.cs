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
    /// <summary>
    /// Example controller
    /// </summary>
    [RbacRule<V1ExampleEntity>(RbacVerb.All, Neon.Kube.Resources.EntityScope.Cluster)]
    [RbacRule<V1Secret>(RbacVerb.List | RbacVerb.Create, Neon.Kube.Resources.EntityScope.Cluster)]
    [RbacRule<V1ServiceMonitor>(RbacVerb.List, Neon.Kube.Resources.EntityScope.Cluster)]
    [RbacRule<V1ServiceAccount>(RbacVerb.List | RbacVerb.Create, Neon.Kube.Resources.EntityScope.Cluster)]
    [RbacRule<V1Node>(RbacVerb.Get | RbacVerb.Watch | RbacVerb.List, Neon.Kube.Resources.EntityScope.Namespaced, Namespace = "foo", ResourceNames = "foo,testaroo,foobar")]
    [RbacRule<V1Service>(RbacVerb.Get | RbacVerb.Watch | RbacVerb.List, Neon.Kube.Resources.EntityScope.Namespaced, Namespace = "foo", ResourceNames = "test,hi,baz")]
    [RbacRule<V1Pod>(RbacVerb.Get | RbacVerb.Watch | RbacVerb.Patch, Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "default")]
    [RbacRule<V1ConfigMap>(RbacVerb.Get | RbacVerb.Watch, Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "default")]
    [RbacRule<V1VirtualService>(RbacVerb.Get | RbacVerb.Watch | RbacVerb.Delete, Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "testaroo")]
    public class ExampleController : IResourceController<V1ExampleEntity>
    {
        private readonly IKubernetes k8s;
        private readonly IFinalizerManager<V1ExampleEntity> finalizerManager;
        private readonly ILogger<ExampleController> logger;

        /// <summary>
        /// Constructor
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
        /// Reconciles
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
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
