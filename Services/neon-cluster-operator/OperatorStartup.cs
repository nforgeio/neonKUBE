//-----------------------------------------------------------------------------
// FILE:	    OperatorStartup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Net.Http;
using System.Text;

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
using Neon.Kube.Resources.Minio;

using NeonClusterOperator.Harbor;

using k8s;
using k8s.Models;

using Minio;

using OpenTelemetry;
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Task = System.Threading.Tasks.Task;
using Metrics = Prometheus.Metrics;

namespace NeonClusterOperator
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
                .AddSingleton(Service.K8s)
                .AddSingleton(Service.DexClient)
                .AddSingleton(Service.HeadendClient)
                .AddSingleton(Service.HarborClient);

            services.AddKubernetesOperator()
                .AddController<GlauthController, V1Secret>()
                .AddController<MinioBucketController, V1MinioBucket>()
                .AddController<NeonClusterOperatorController, V1NeonClusterOperator>()
                .AddController<NeonContainerRegistryController, V1NeonContainerRegistry>()
                .AddController<NeonDashboardController, V1NeonDashboard>()
                .AddController<NeonSsoClientController, V1NeonSsoClient>()
                .AddController<NodeTaskController, V1NeonNodeTask>()
                .AddFinalizer<NeonContainerRegistryFinalizer, V1NeonContainerRegistry>()
                .AddFinalizer<NeonSsoClientFinalizer, V1NeonSsoClient>()
                .AddFinalizer<MinioBucketFinalizer, V1MinioBucket>()
                .AddMutatingWebhook<PodWebhook, V1Pod>()
                .AddValidatingWebhook<NeonSsoConnectorValidatingWebhook, V1NeonSsoConnector>()
                .AddNgrokTunnnel(hostname: Service.GetEnvironmentVariable("NGROK_HOSTNAME", def: "127.0.0.1", redact: false),
                    port: Service.Port,
                    ngrokDirectory: Service.GetEnvironmentVariable("NGROK_DIRECTORY", def: "C:/bin", redact: false),
                    ngrokAuthToken: Service.GetEnvironmentVariable("NGROK_AUTH_TOKEN", def: null, redact: true),
                    enabled: NeonHelper.IsDevWorkstation);
        }

        /// <summary>
        /// Configures the operator service controllers.
        /// </summary>
        /// <param name="app">Specifies the application builder.</param>
        public void Configure(IApplicationBuilder app)
        {
            if (NeonHelper.IsDevWorkstation
                || !string.IsNullOrEmpty(Service.GetEnvironmentVariable("DEBUG")))
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseKubernetesOperator();
        }
    }
}
