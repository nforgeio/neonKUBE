//-----------------------------------------------------------------------------
// FILE:	    Service.cs
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

using System.Threading.Tasks;
using System.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Resources.Dex;
using Neon.Service;
using Neon.Tasks;

using Prometheus;
using Prometheus.DotNetRuntime;

using k8s;
using k8s.Models;

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
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        public Service(string name)
             : base(name, version: KubeVersions.NeonKube, new NeonServiceOptions() { MetricsPrefix = "neonssosessionproxy" })
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

            KubeHelper.InitializeJson(); 
            
            k8s = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig(), new KubernetesRetryHandler());

            if (NeonHelper.IsDevWorkstation)
            {
                var configFile = await k8s.CoreV1.ReadNamespacedConfigMapAsync("neon-sso-dex", KubeNamespace.NeonSystem);

                Config = NeonHelper.YamlDeserializeViaJson<DexConfig>(configFile.Data["config.yaml"]);
            }
            else
            {
                var configFile = GetConfigFilePath("/etc/neonkube/neon-sso-session-proxy/config.yaml");
                using (StreamReader reader = new StreamReader(new FileStream(configFile, FileMode.Open, FileAccess.Read)))
                {
                    Config = NeonHelper.YamlDeserializeViaJson<DexConfig>(await reader.ReadToEndAsync());
                }
            }

            // Dex config
            if (NeonHelper.IsDevWorkstation)
            {
                DexClient = new DexClient(new Uri($"http://localhost:5556"), Logger);

            }
            else
            {
                DexClient = new DexClient(new Uri($"http://{KubeService.Dex}:5556"), Logger);
            }

            Clients = new List<V1NeonSsoClient>();

            _ = k8s.WatchAsync<V1NeonSsoClient>(async (@event) =>
            {
                await SyncContext.Clear;

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
            });

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

            Clients.Remove(
                Clients.Where(
                    c => c.Spec.Id == client.Spec.Id
                    && c.Spec.Secret == client.Spec.Secret).First());

            DexClient.AuthHeaders.Remove(client.Spec.Id);

            Logger.LogDebugEx(() => $"Removed client: {client.Name()}");
        }
    }
}