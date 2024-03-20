//-----------------------------------------------------------------------------
// FILE:        Service.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright � 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using k8s;
using k8s.Models;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Diagnostics;
using Neon.K8s;
using Neon.Kube;
using Neon.Kube.PortForward;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Resources.Dex;
using Neon.Net;
using Neon.Service;
using Neon.Tasks;

using Prometheus;
using Prometheus.DotNetRuntime;

using OpenTelemetry.Trace;
using OpenTelemetry;

namespace NeonSsoSessionProxy
{
    /// <summary>
    /// Implements the <b>neon-sso-session-proxy</b> service.
    /// </summary>
    public class Service : NeonService
    {
        // class fields
        private IWebHost webHost;
        private IKubernetes k8s;
        
        /// <summary>
        /// Session cookie name.
        /// </summary>
        public const string SessionCookieName = ".NeonKUBE.SsoProxy.Session.Cookie";

        /// <summary>
        /// The Dex configuration.
        /// </summary>
        public DexConfig Config { get; private set; }

        /// <summary>
        /// The Dex client.
        /// </summary>
        public DexClient DexClient { get; private set; }

        /// <summary>
        /// Clients available.
        /// </summary>
        public List<V1NeonSsoClient> Clients;

        /// <summary>
        /// The port forward manager used for debugging in Visual Studio.
        /// </summary>
        public PortForwardManager PortForwardManager;

        /// <summary>
        /// The port used to communicate with Dex.
        /// </summary>
        public Uri DexUri { get; private set; }

        /// <summary>
        /// The port used to communicate with Dex.
        /// </summary>
        public int DexPort { get; private set; } = 5556;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        public Service(string name)
             : base(name, version: KubeVersions.NeonKube)
        {   
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

            Neon.Kube.KubeHelper.InitializeJson(); 
            
            k8s = Neon.Kube.KubeHelper.CreateKubernetesClient();

            if (NeonHelper.IsDevWorkstation)
            {
                var configFile = await k8s.CoreV1.ReadNamespacedConfigMapAsync("neon-sso-dex", KubeNamespace.NeonSystem);

                Config = NeonHelper.YamlDeserializeViaJson<DexConfig>(configFile.Data["config.yaml"]);

                PortForwardManager = new PortForwardManager(k8s, TelemetryHub.LoggerFactory);

                var pod       = (await k8s.CoreV1.ListNamespacedPodAsync(KubeNamespace.NeonSystem, labelSelector: "app.kubernetes.io/name=dex")).Items.First();
                DexPort       = NetHelper.GetUnusedTcpPort(IPAddress.Loopback);
                DexUri        = new Uri($"http://{IPAddress.Loopback}:{DexPort}");

                PortForwardManager.StartPodPortForward(
                    name: pod.Name(),
                    @namespace: KubeNamespace.NeonSystem,
                    localPort: DexPort,
                    remotePort: 5556);

                DexClient = new DexClient(DexUri, Logger);

            }
            else
            {
                var configFile = GetConfigFilePath("/etc/neonkube/neon-sso-session-proxy/config.yaml");

                using (StreamReader reader = new StreamReader(new FileStream(configFile, FileMode.Open, FileAccess.Read)))
                {
                    Config = NeonHelper.YamlDeserializeViaJson<DexConfig>(await reader.ReadToEndAsync());
                }

                DexUri    = new Uri($"http://{KubeService.Dex}:{DexPort}");
                DexClient = new DexClient(DexUri, Logger);
            }

            Clients = new List<V1NeonSsoClient>();

            _ = k8s.WatchAsync<V1NeonSsoClient>(
                async (@event) =>
                {
                    switch (@event.Type)
                    {
                        case WatchEventType.Added:

                            await AddClientAsync(@event.Value);
                            break;

                        case WatchEventType.Deleted:

                            await RemoveClientAsync(@event.Value);
                            break;

                        case WatchEventType.Modified:

                            await RemoveClientAsync(@event.Value);
                            await AddClientAsync(@event.Value);
                            break;

                        default:

                            break;
                    }
                },
                retryDelay: TimeSpan.FromSeconds(30),
                logger:     Logger);

            int port = 80;

            if (NeonHelper.IsDevWorkstation)
            {
                port = 11055;
            }

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

            // Indicate that the service is ready for business.

            await SetStatusAsync(NeonServiceStatus.Running);

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
                        options.ExportProcessorType         = ExportProcessorType.Batch;
                        options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>();
                        options.Endpoint                    = new Uri(NeonHelper.NeonKubeOtelCollectorUri);
                        options.Protocol                    = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });

            return true;
        }

        private async Task AddClientAsync(V1NeonSsoClient client)
        {
            await SyncContext.Clear;

            Clients.Add(client);
            
            DexClient.AuthHeaders.Add(client.Spec.Id, new BasicAuthenticationHeaderValue(client.Spec.Id, client.Spec.Secret));

            Logger.LogDebugEx(() => $"Added client: {client.Name()}");
        }

        private async Task RemoveClientAsync(V1NeonSsoClient client)
        {
            await SyncContext.Clear;

            Clients.Remove(Clients.Where(client => client.Spec.Id == client.Spec.Id && client.Spec.Secret == client.Spec.Secret).First());
            DexClient.AuthHeaders.Remove(client.Spec.Id);

            Logger.LogDebugEx(() => $"Removed client: {client.Name()}");
        }
    }
}
