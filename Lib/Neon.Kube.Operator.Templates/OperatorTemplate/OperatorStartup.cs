using System;
using System.Net.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;

using k8s;
using k8s.Models;

namespace OperatorTemplate
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
        /// The <see cref="Service"/>.
        /// </summary>
        public Service Service;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Specifies the service configuration.</param>
        /// <param name="service">Specifies the service.</param>
        public OperatorStartup(IConfiguration configuration, Service service)
        {
            this.Configuration = configuration;
            this.Service = service;
        }

        /// <summary>
        /// Configures depdendency injection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ILogger>(Program.Service.Logger)
                .AddSingleton(Service.K8s);

            services.AddKubernetesOperator()
                .AddController<ContainerRegistryController>(
                    leaderConfig: new LeaderElectionConfig(
                        k8s:           Service.K8s,
                        @namespace:    KubeNamespace.NeonSystem,
                        leaseName:     $"{Program.Service.Name}.containerregistry-{Node.Name}",
                        identity:      Pod.Name,
                        metricsPrefix: $"{typeof(ContainerRegistryController).Name}_{typeof(V1NeonContainerRegistry).Name}".ToLower()))

                .AddController<NodeTaskController>(
                    leaderConfig:
                        new LeaderElectionConfig(
                        k8s:           Service.K8s,
                        @namespace:    KubeNamespace.NeonSystem,
                        leaseName:     $"{Program.Service.Name}.nodetask-{Node.Name}",
                        identity:      Pod.Name,
                        metricsPrefix: $"{typeof(ContainerRegistryController).Name}_{typeof(V1NeonContainerRegistry).Name}".ToLower()));
        }

        /// <summary>
        /// Configures the operator service controllers.
        /// </summary>
        /// <param name="app">Specifies the application builder.</param>
        public void Configure(IApplicationBuilder app)
        {
            if (NeonHelper.IsDevWorkstation)
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseKubernetesOperator();
        }
    }
}
