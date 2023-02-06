//-----------------------------------------------------------------------------
// FILE:	    NgrokWebhookTunnel.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;

using k8s.Models;
using k8s;

using NgrokSharp;
using Neon.Common;
using System.Net.Http;
using NgrokSharp.DTO;
using Neon.Net;
using Neon.Kube.Operator.Webhook.Ngrok;
using System.Diagnostics;
using Neon.Tasks;
using Neon.Kube.Operator.Builder;

namespace Neon.Kube.Operator.Webhook.Ngrok
{
    /// <summary>
    /// <para>
    /// Provides an Ngrok tunnel for debugging webhooks.
    /// </para>
    /// </summary>
    internal class NgrokWebhookTunnel : IHostedService, IDisposable
    {
        /// <summary>
        /// The host that the tunnel should connect to.
        /// </summary>
        public string Host { get; init; } = string.Empty;

        /// <summary>
        /// The port that the tunnel should connect to.
        /// </summary>
        public int Port { get; init; }

        /// <summary>
        /// Details about the current tunnel.
        /// </summary>
        public NgrokTunnelDetail Tunnel { get; private set; }

        private readonly ILogger logger;
        private readonly IKubernetes k8s;
        private readonly INgrokManager ngrokManager;
        private readonly ComponentRegister componentRegister;
        private readonly IServiceProvider serviceProvider;
        private readonly JsonClient jsonClient;
        private string tunnelNname;
        private string ngrokdirectory;
        private string ngrokAuthToken;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s"></param>
        /// <param name="componentRegister"></param>
        /// <param name="serviceProvider"></param>
        /// <param name="ngrokdirectory"></param>
        /// <param name="ngrokAuthToken"></param>
        public NgrokWebhookTunnel(
            IKubernetes k8s,
            ComponentRegister componentRegister,
            IServiceProvider serviceProvider,
            string ngrokdirectory = null,
            string ngrokAuthToken = null)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(componentRegister != null, nameof(componentRegister));
            Covenant.Requires(serviceProvider != null, nameof(serviceProvider));

            this.k8s               = k8s;
            this.componentRegister = componentRegister;
            this.serviceProvider   = serviceProvider;
            this.ngrokdirectory    = ngrokdirectory;
            this.ngrokAuthToken    = ngrokAuthToken;
            this.logger            = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<NgrokWebhookTunnel>();

            ngrokManager = new NgrokManager();

            jsonClient = new JsonClient()
            {
                BaseAddress = new Uri("http://localhost:4040")
            };

            tunnelNname = NeonHelper.CreateBase36Uuid();


        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                ngrokManager.StopNgrok();
            }
            catch (Exception e)
            {
                logger?.LogErrorEx(e);
            }
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await KillExistingNgrokProcessesAsync();

                if (!string.IsNullOrEmpty(ngrokdirectory))
                {
                    ngrokManager.SetNgrokDirectory(ngrokdirectory.TrimEnd('/') + '/');
                }

                if (!string.IsNullOrEmpty(ngrokAuthToken))
                {
                    await ngrokManager.RegisterAuthTokenAsync(ngrokAuthToken);
                }

                ngrokManager.StartNgrok();

                var tunnel = new NgrokTunnelRequest
                {
                    Name = tunnelNname,
                    Proto = "http",
                    Addr = Port.ToString()
                };

                Tunnel = await jsonClient.PostAsync<NgrokTunnelDetail>("api/tunnels", tunnel);

                try
                {
                    var url = Tunnel.PublicUrl.ToString();
                }
                catch
                {

                }

                var componentRegistrar = serviceProvider.GetRequiredService<ComponentRegister>();

                foreach (var mutatingWebhookRegistration in componentRegister.MutatingWebhookRegistrations)
                {
                    var mutator = serviceProvider.GetRequiredService(mutatingWebhookRegistration.WebhookType);

                    var createMethod = typeof(IMutatingWebhook<>)
                                    .MakeGenericType(mutatingWebhookRegistration.EntityType)
                                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                    .First(m => m.Name == "Create");

                    await (Task)createMethod.Invoke(mutator, new object[] { k8s, serviceProvider.GetService<ILoggerFactory>() });

                    var property = typeof(IMutatingWebhook<>)
                                .MakeGenericType(mutatingWebhookRegistration.EntityType)
                                .GetProperty("WebhookConfiguration");

                    var config = (V1MutatingWebhookConfiguration)property.GetValue(mutator);

                    var webhook = await k8s.AdmissionregistrationV1.ReadMutatingWebhookConfigurationAsync(config.Name());

                    webhook.SetAnnotation("cert-manager.io/inject-ca-from", null);

                    foreach (var hook in webhook.Webhooks)
                    {
                        var path = hook.ClientConfig.Service.Path;

                        hook.ClientConfig.Service = null;
                        hook.ClientConfig.CaBundle = null;
                        hook.ClientConfig.Url = Tunnel.PublicUrl.TrimEnd('/') + path;
                    }

                    await k8s.AdmissionregistrationV1.ReplaceMutatingWebhookConfigurationAsync(webhook, webhook.Name());
                }

                foreach (var validatingWebhookRegistration in componentRegister.ValidatingWebhookRegistrations)
                {
                    var validator = serviceProvider.GetRequiredService(validatingWebhookRegistration.WebhookType);

                    var createMethod = typeof(IValidatingWebhook<>)
                                    .MakeGenericType(validatingWebhookRegistration.EntityType)
                                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                    .First(m => m.Name == "Create");

                    await (Task)createMethod.Invoke(validator, new object[] { k8s, serviceProvider.GetService<ILoggerFactory>() });

                    var property = typeof(IValidatingWebhook<>)
                                .MakeGenericType(validatingWebhookRegistration.EntityType)
                                .GetProperty("WebhookConfiguration");

                    var config = (V1ValidatingWebhookConfiguration)property.GetValue(validator);

                    var webhook = await k8s.AdmissionregistrationV1.ReadValidatingWebhookConfigurationAsync(config.Name());

                    webhook.SetAnnotation("cert-manager.io/inject-ca-from", null);

                    foreach (var hook in webhook.Webhooks)
                    {
                        var path = hook.ClientConfig.Service.Path;

                        hook.ClientConfig.Service = null;
                        hook.ClientConfig.CaBundle = null;
                        hook.ClientConfig.Url = Tunnel.PublicUrl.TrimEnd('/') + path;
                    }

                    await k8s.AdmissionregistrationV1.ReplaceValidatingWebhookConfigurationAsync(webhook, webhook.Name());
                }
            }
            catch (Exception e)
            {
                logger?.LogErrorEx(e);
                Tunnel = null;
                return;
            }
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                ngrokManager.StopNgrok();
            }
            catch (Exception e)
            {
                logger?.LogErrorEx(e);
            }

            return Task.CompletedTask;
        }

        public async Task KillExistingNgrokProcessesAsync()
        {
            await SyncContext.Clear;

            foreach (var p in Process.GetProcessesByName("ngrok"))
            {
                p.Kill();
            }
        }
    }
}
