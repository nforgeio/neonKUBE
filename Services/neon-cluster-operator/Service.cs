//------------------------------------------------------------------------------
// FILE:        Service.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.Glauth;
using Neon.Kube.Operator;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Resources;
using Neon.Kube.Resources.CertManager;
using Neon.Net;
using Neon.Kube.PortForward;
using Neon.Retry;
using Neon.Service;
using Neon.Tasks;

using NeonClusterOperator.Harbor;

using DnsClient;

using Grpc.Net.Client;

using k8s;
using k8s.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Npgsql;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.Quartz;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz;
using Quartz.Impl;
using Quartz.Logging;
using Minio;

using Task    = System.Threading.Tasks.Task;
using Metrics = Prometheus.Metrics;

namespace NeonClusterOperator
{
    /// <summary>
    /// Implements the <b>neon-cluster-operator</b> service.
    /// </summary>
    /// <remarks>
    /// <para><b>ENVIRONMENT VARIABLES</b></para>
    /// <para>
    /// The <b>neon-node-agent</b> is configured using these environment variables:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>WATCHER_TIMEOUT_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum time the resource watcher will wait without
    ///     a response before creating a new request.  This defaults to <b>2 minutes</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>WATCHER_MAX_RETRY_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum time the resource watcher will wait
    ///     after a watch failure.  This defaults to <b>15 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_IDLE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the interval at which IDLE events will be raised
    ///     for <b>NodeTask</b> giving the operator the chance to delete node tasks assigned
    ///     to nodes that don't exist.  This defaults to <b>60 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the minimum requeue interval to use when an
    ///     exception is thrown when handling NodeTask events.  This
    ///     value will be doubled when subsequent events also fail until the
    ///     requeue time maxes out at <b>CONTAINERREGISTRY_ERROR_MIN_REQUEUE_INTERVAL</b>.
    ///     This defaults to <b>5 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum requeue time for NodeTask
    ///     handler exceptions.  This defaults to <b>60 seconds</b>.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    [RbacRule<V1ConfigMap>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1Secret>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1Pod>(Verbs = RbacVerb.List, Scope = EntityScope.Namespaced, Namespace = KubeNamespace.NeonSystem)]
    public partial class Service : NeonService
    {
        private const int dexPort = 5557;

        private HttpClient                      harborHttpClient;
        private readonly JsonSerializerOptions  serializeOptions;

        /// <summary>
        /// Information about the cluster.
        /// </summary>
        public ClusterInfo ClusterInfo;

        /// <summary>
        /// Kubernetes client.
        /// </summary>
        public IKubernetes K8s;

        /// <summary>
        /// Kubernetes client.
        /// </summary>
        public HeadendClient HeadendClient;

        /// <summary>
        /// Harbor client.
        /// </summary>
        public HarborClient HarborClient;

        /// <summary>
        /// Dex client.
        /// </summary>
        public Dex.Dex.DexClient DexClient;

        /// <summary>
        /// The port forward manager used for debugging in Visual Studio.
        /// </summary>
        public PortForwardManager PortForwardManager;

        // private fields

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        public Service(string name)
            : base(name, version: KubeVersions.NeonKube)
        {
            serializeOptions = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            serializeOptions.Converters.Add(new JsonStringEnumMemberConverter());
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            K8s = KubeHelper.GetKubernetesClient();
            
            LogContext.SetCurrentLogProvider(TelemetryHub.LoggerFactory);

            if (NeonHelper.IsDevWorkstation)
            {
                this.PortForwardManager = new PortForwardManager(K8s, TelemetryHub.LoggerFactory);
            }

            await WatchClusterInfoAsync();
            await ConfigureDexAsync();
            await ConfigureHarborAsync();

            HeadendClient = HeadendClient.Create();
            HeadendClient.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetEnvironmentVariable("NEONCLOUD_HEADEND_TOKEN"));

            // Start the web service.

            var k8s = KubernetesOperatorHost
               .CreateDefaultBuilder()
               .ConfigureOperator(configure =>
               {
                   configure.AssemblyScanningEnabled = true;
                   configure.Name                    = Name;
                   configure.DeployedNamespace       = KubeNamespace.NeonSystem;
               })
               .ConfigureNeonKube()
               .AddSingleton(typeof(Service), this)
               .UseStartup<OperatorStartup>()
               .Build();

            _ = k8s.RunAsync();

            // Indicate that the service is running.

            await StartedAsync();

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }

        /// <inheritdoc/>
        protected override bool OnLoggerConfg(OpenTelemetryLoggerOptions options)
        {
            if (NeonHelper.IsDevWorkstation || !string.IsNullOrEmpty(GetEnvironmentVariable("DEBUG")))
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: Name, serviceVersion: Version));

                options.AddConsoleTextExporter(options =>
                {
                    options.Format = (record) => $"[{record.LogLevel}][{record.CategoryName}] {record.FormattedMessage}";
                });

                return true;
            }

            return false;
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

                        // Filter out leader election since it's really chatty.

                        if (httpcontext.RequestUri.Host == "10.253.0.1")
                        {
                            return false;
                        }

                        return true;
                    };
                })
                .AddAspNetCoreInstrumentation()
                .AddGrpcCoreInstrumentation()
                .AddKubernetesOperatorInstrumentation()
                .AddNpgsql()
                .AddQuartzInstrumentation()
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

        /// <summary>
        /// Starts the <see cref="ClusterInfo"/> watcher.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TimeoutException">Thrown if the cluster information could not be retrieved after a grace period.</exception>
        private async Task WatchClusterInfoAsync()
        {
            await SyncContext.Clear;

            //###########################################################################
            // $todo(jefflill): Remove this hack once we've figured out the watcher issue.
            //
            // We need to ensure that we have the initial cluster information before this
            // method returns.
            //
            // Marcus originally started the watcher and then used a [WaitFor()] call to
            // wait for the watcher to report and set the [ClusterInfo] property.  Unfortunately,
            // watchers don't seem to always report on objects that already exist.  We're
            // going to hack around this for now by explicitly waiting for the cluster info
            // before staring the watcher.

            var retry = new LinearRetryPolicy(e => true, retryInterval: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(60));

            ClusterInfo = await retry.InvokeAsync(async () => (await K8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterInfo>(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus)).Data);

            //###########################################################################

            // Start the watcher.

            _ = K8s.WatchAsync<V1ConfigMap>(
                async (@event) =>
                {
                    await SyncContext.Clear;

                    ClusterInfo = TypedConfigMap<ClusterInfo>.From(@event.Value).Data;

                    Logger.LogInformationEx("Updated cluster info");
                },
                KubeNamespace.NeonStatus,
                fieldSelector: $"metadata.name={KubeConfigMapName.ClusterInfo}");

            // Wait for the watcher to see the [ClusterInfo].

            NeonHelper.WaitFor(() => ClusterInfo != null, timeout: TimeSpan.FromSeconds(60), timeoutMessage: "Timeout obtaining: cluster-info.");
        }

        /// <summary>
        /// Configures DEX.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ConfigureDexAsync()
        {
            await SyncContext.Clear;

            GrpcChannel channel;

            if (!NeonHelper.IsDevWorkstation)
            {
                channel = GrpcChannel.ForAddress($"http://{KubeService.Dex}:{dexPort}");
            }
            else
            {
                var pod       = (await K8s.CoreV1.ListNamespacedPodAsync(KubeNamespace.NeonSystem, labelSelector: "app.kubernetes.io/name=dex")).Items.First();
                var localPort = NetHelper.GetUnusedTcpPort(IPAddress.Loopback);
                
                PortForwardManager.StartPodPortForward(
                    name:       pod.Name(),
                    @namespace: KubeNamespace.NeonSystem,
                    localPort:  localPort, 
                    remotePort: dexPort);

                channel = GrpcChannel.ForAddress($"http://localhost:{localPort}");
            }

            DexClient = new Dex.Dex.DexClient(channel);
        }

        /// <summary>
        /// Configures Harbor.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ConfigureHarborAsync()
        {
            await SyncContext.Clear;

            harborHttpClient = new HttpClient(new HttpClientHandler() { UseCookies = false });
            HarborClient     = new HarborClient(harborHttpClient);

            if (!NeonHelper.IsDevWorkstation)
            {
                HarborClient.BaseUrl = "http://registry-harbor-harbor-core.neon-system/api/v2.0";
            }
            else
            {
                HarborClient.BaseUrl = $"https://neon-registry.{ClusterInfo.Domain}/api/v2.0";
            }

            _ = K8s.WatchAsync<V1Secret>(
                async (@event) =>
                {
                    var rootUser   = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(@event.Value.Data["root"]));
                    var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{rootUser.Name}:{rootUser.Password}"));

                    harborHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

                    Logger.LogInformationEx("Updated Harbor Client");
                    await Task.CompletedTask;
                },
                KubeNamespace.NeonSystem,
                fieldSelector: $"metadata.name=glauth-users");
        }
    }
}