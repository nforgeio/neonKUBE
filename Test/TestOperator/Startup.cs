using System.Collections.Generic;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Neon.Kube.Operator;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Operator.ResourceManager;

using k8s.Models;

namespace TestOperator
{
    /// <summary>
    /// Configures the operator's service controllers.
    /// </summary>
    public class OperatorStartup
    {
        /// <summary>
        /// The <see cref="IConfiguration"/>.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Specifies the service configuration.</param>
        public OperatorStartup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        /// <summary>
        /// Configures depdendency injection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging()
                .AddKubernetesOperator()
                .AddController<ExampleController>(
                    options: new Neon.Kube.Operator.ResourceManager.ResourceManagerOptions()
                    {
                        DependentResources = new List<Neon.Kube.Operator.ResourceManager.IDependentResource>()
                        {
                            new DependentResource<V1Pod>() {Scope = Neon.Kube.Resources.EntityScope.Namespaced}
                        },
                        RbacRules = new List<IRbacRule>()
                        {
                            new RbacRule<V1DaemonSet>(verbs: RbacVerb.Watch, scope: Neon.Kube.Resources.EntityScope.Cluster),
                            new RbacRule<V1StatefulSet>(verbs: RbacVerb.Get, scope: Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "default,services,marcus"),
                            new RbacRule<V1StatefulSet>(verbs: RbacVerb.Patch, scope: Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "services"),
                            new RbacRule<V1StatefulSet>(verbs: RbacVerb.List, scope: Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "marcus"),
                            new RbacRule<V1Deployment>(verbs: RbacVerb.Get, scope: Neon.Kube.Resources.EntityScope.Namespaced, @namespace: "default"),
                        }
                    });
        }

        /// <summary>
        /// Configures the operator service controllers.
        /// </summary>
        /// <param name="app">Specifies the application builder.</param>
        public void Configure(IApplicationBuilder app)
        {
            app.UseKubernetesOperator();
        }
    }
}
