//-----------------------------------------------------------------------------
// FILE:	    Service.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Resources.Istio;
using Neon.Service;
using Neon.Tasks;

using NeonDashboard.Shared.Components;

using k8s;
using k8s.Models;

using Prometheus;

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace NeonDashboard
{
    /// <summary>
    /// Implements the <b>neon-dashboard</b> service.
    /// </summary>
    public class Service : NeonService
    {
        // class fields
        private IWebHost webHost;

        /// <summary>
        /// The Kubernetes client.
        /// </summary>
        public IKubernetes Kubernetes;

        /// <summary>
        /// Information about the cluster.
        /// </summary>
        public ClusterInfo ClusterInfo;

        /// <summary>
        /// Dashboards available.
        /// </summary>
        public List<Dashboard> Dashboards;

        /// <summary>
        /// SSO Client Secret.
        /// </summary>
        public string SsoClientSecret;

        /// <summary>
        /// AES Cipher for protecting cookies..
        /// </summary>
        public AesCipher AesCipher;

        /// <summary>
        /// USe to turn off Segment tracking.
        /// </summary>
        public bool DoNotTrack;

        /// <summary>
        /// Prometheus Client.
        /// </summary>
        public PrometheusClient PrometheusClient;

        /// <summary>
        /// Session cookie name.
        /// </summary>
        public const string sessionCookieName = ".NeonKUBE.Dashboard.Session.Cookie";

        /// <summary>
        /// Dashboard view counter.
        /// </summary>
        public readonly Counter DashboardViewCounter;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        public Service(string name)
             : base(name, version: KubeVersions.NeonKube)
        {
            DashboardViewCounter = Metrics.CreateCounter($"neondashboard_external_dashboard_view", "External dashboard views.",
                new CounterConfiguration
                {
                    LabelNames = new[] { "dashboard" }
                });
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // Dispose web host if it's still running.

            if (webHost != null)
            {
                webHost.Dispose();
                webHost = null;
            }
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            await SetStatusAsync(NeonServiceStatus.Starting);

            var port = 80;

            KubeHelper.InitializeJson();

            Kubernetes = KubeHelper.GetKubernetesClient();

            var metricsHost = GetEnvironmentVariable("METRICS_HOST", $"http://{KubeService.MimirQueryFrontend}.{KubeNamespace.NeonMonitor}:8080");
            
            PrometheusClient = new PrometheusClient($"{metricsHost}/prometheus/");

            _ = Kubernetes.WatchAsync<V1ConfigMap>(
                async (@event) =>
                {
                    ClusterInfo = TypedConfigMap<ClusterInfo>.From(@event.Value).Data;
                
                    if (PrometheusClient.JsonClient.DefaultRequestHeaders.Contains("X-Scope-OrgID"))
                    {
                        PrometheusClient.JsonClient.DefaultRequestHeaders.Remove("X-Scope-OrgID");
                    }

                    PrometheusClient.JsonClient.DefaultRequestHeaders.Add("X-Scope-OrgID", ClusterInfo.Name);
                    Logger.LogInformationEx("Updated cluster info");
                    await Task.CompletedTask;
                },
                KubeNamespace.NeonStatus,
                fieldSelector: $"metadata.name={KubeConfigMapName.ClusterInfo}",
                logger: Logger);

            Dashboards = new List<Dashboard>();
            Dashboards.Add(
                new Dashboard(
                    id:           "neonkube", 
                    name:         "NEONKUBE",
                    displayOrder: 0));

            _ = Kubernetes.WatchAsync<V1NeonDashboard>(
                async (@event) =>
                {
                    await SyncContext.Clear;

                    switch (@event.Type)
                    {
                        case WatchEventType.Added:

                            await AddDashboardAsync(@event.Value);
                            break;

                        case WatchEventType.Deleted:

                            await RemoveDashboardAsync(@event.Value);
                            break;

                        case WatchEventType.Modified:

                            await RemoveDashboardAsync(@event.Value);
                            await AddDashboardAsync(@event.Value);
                            break;

                        default:

                            break;
                    }

                    Dashboards = Dashboards.OrderBy(d => d.DisplayOrder)
                                            .ThenBy(d => d.Name).ToList();
                },
                logger: Logger);

            if (NeonHelper.IsDevWorkstation)
            {
                port = 11001;
                SetEnvironmentVariable("LOG_LEVEL", "debug");
                SetEnvironmentVariable("DO_NOT_TRACK", "true");
                SetEnvironmentVariable("COOKIE_CIPHER", "/HwPfpfACC70Rh1DeiMdubHINQHRGfc4JP6DYcSkAQ8=");
                await ConfigureDevAsync();
            }

            SsoClientSecret = GetEnvironmentVariable("SSO_CLIENT_SECRET", redact: true);
            AesCipher       = new AesCipher(GetEnvironmentVariable("COOKIE_CIPHER", AesCipher.GenerateKey(), redact: true));

            // Start the web service.

            webHost = new WebHostBuilder()
                .ConfigureAppConfiguration(
                    (hostingcontext, config) =>
                    {
                        config.Sources.Clear();
                    })
                .UseStartup<Startup>()
                .UseKestrel(options => options.Listen(IPAddress.Any, port))
                .ConfigureServices(services => services.AddSingleton(typeof(Service), this))
                .UseStaticWebAssets()
                .Build();

            _ = webHost.RunAsync();

            Logger.LogInformationEx(() => $"Listening on {IPAddress.Any}:{port}");

            // Indicate that the service is running.

            await StartedAsync();

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }

        /// <inheritdoc/>
        protected override bool OnTracerConfig(TracerProviderBuilder builder)
        {
            builder.AddHttpClientInstrumentation(
                options =>
                {
                    options.FilterHttpRequestMessage = (httpcontext) =>
                    {
                        if (GetEnvironmentVariable("LOG_LEVEL").ToLower() == "trace")
                        {
                            return true;
                        }

                        if (httpcontext.RequestUri.Host == "10.253.0.1")
                        {
                            return false;
                        }

                        return true;
                    };
                })
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(
                    options =>
                    {
                        options.ExportProcessorType = ExportProcessorType.Batch;
                        options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>();
                        options.Endpoint = new Uri(NeonHelper.NeonKubeOtelCollectorUri);
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });

            return true;
        }

        private async Task AddDashboardAsync(V1NeonDashboard dashboard)
        {
            await SyncContext.Clear;

            if (string.IsNullOrEmpty(dashboard.Spec.DisplayName))
            {
                dashboard.Spec.DisplayName = dashboard.Name();
            }

            Dashboards.Add(
                new Dashboard(
                    id:           dashboard.Name(),
                    name:         dashboard.Spec.DisplayName,
                    url:          dashboard.Spec.Url,
                    displayOrder: dashboard.Spec.DisplayOrder));
        }
        private async Task RemoveDashboardAsync(V1NeonDashboard dashboard)
        {
            await SyncContext.Clear;

            Dashboards.Remove(
                Dashboards.Where(
                    d => d.Id == dashboard.Name())?.First());
        }

        public async Task ConfigureDevAsync()
        {
            await SyncContext.Clear;

            Logger.LogInformationEx("Configuring cluster SSO for development.");

            // Wait for cluster info to be set.

            NeonHelper.WaitFor(() => ClusterInfo != null,
                timeout:      TimeSpan.FromSeconds(60),
                pollInterval: TimeSpan.FromMilliseconds(250));

            try
            {
                var secret = await Kubernetes.CoreV1.ReadNamespacedSecretAsync("neon-sso-dex", KubeNamespace.NeonSystem);

                SetEnvironmentVariable("SSO_CLIENT_SECRET", Encoding.UTF8.GetString(secret.Data["NEONSSO_CLIENT_SECRET"]));

                // Configure cluster callback url to allow local dev

                var ssoClient = await Kubernetes.CustomObjects.ReadClusterCustomObjectAsync<V1NeonSsoClient>("neon-sso");

                if (!ssoClient.Spec.RedirectUris.Contains("http://localhost:11001/oauth2/callback"))
                {
                    ssoClient.Spec.RedirectUris.Add("http://localhost:11001/oauth2/callback");
                    await Kubernetes.CustomObjects.UpsertClusterCustomObjectAsync<V1NeonSsoClient>(ssoClient, ssoClient.Name());
                }

                Logger.LogInformationEx("SSO configured.");
            }
            catch (Exception e)
            {
                Logger.LogErrorEx(e, "Error configuring SSO");
            }

            Logger.LogInformationEx("Configure metrics.");

            var virtualServices = await Kubernetes.CustomObjects.ListNamespacedCustomObjectAsync<V1VirtualService>(KubeNamespace.NeonIngress);
            if (!virtualServices.Items.Any(vs => vs.Name() == "metrics-external"))
            {
                var virtualService = new V1VirtualService()
                {
                    Metadata = new V1ObjectMeta()
                    {
                        Name = "metrics-external",
                        NamespaceProperty = KubeNamespace.NeonIngress
                    },
                    Spec = new V1VirtualServiceSpec()
                    {
                        Gateways = new List<string>() { "neoncluster-gateway" },
                        Hosts = new List<string>() { $"metrics.{ClusterInfo.Domain}" },
                        Http = new List<HTTPRoute>()
                    {
                        new HTTPRoute()
                        {
                            Match = new List<HTTPMatchRequest>()
                            {
                                new HTTPMatchRequest()
                                {
                                    Uri = new StringMatch()
                                    {
                                        Prefix = "/"
                                    }
                                }
                            },
                            Route = new List<HTTPRouteDestination>()
                            {
                                new HTTPRouteDestination()
                                {
                                    Destination = new Destination()
                                    {
                                        Host = "mimir-query-frontend.neon-monitor.svc.cluster.local",
                                        Port = new PortSelector()
                                        {
                                            Number = 8080
                                        }
                                    }
                                }
                            }
                        }
                    }
                    }
                };

                await Kubernetes.CustomObjects.CreateNamespacedCustomObjectAsync<V1VirtualService>(
                    body:               virtualService, 
                    name:               virtualService.Name(),
                    namespaceParameter: KubeNamespace.NeonIngress);
            }

            PrometheusClient = new PrometheusClient($"https://metrics.{ClusterInfo.Domain}/prometheus/");

            if (PrometheusClient.JsonClient.DefaultRequestHeaders.Contains("X-Scope-OrgID"))
            {
                PrometheusClient.JsonClient.DefaultRequestHeaders.Remove("X-Scope-OrgID");
            }

            PrometheusClient.JsonClient.DefaultRequestHeaders.Add("X-Scope-OrgID", ClusterInfo.Name);
        }
    }
}