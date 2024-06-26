//------------------------------------------------------------------------------
// FILE:        Service.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Grpc.Net.Client;

using k8s;
using k8s.Models;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Diagnostics;
using Neon.K8s;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.Glauth;
using Neon.Kube.PortForward;
using Neon.Net;
using Neon.Operator;
using Neon.Operator.Attributes;
using Neon.Operator.Rbac;
using Neon.Retry;
using Neon.Service;
using Neon.Tasks;

using NeonClusterOperator.Harbor;

using Newtonsoft.Json.Linq;

using Npgsql;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Quartz.Logging;

using KubeHelper = Neon.Kube.KubeHelper;
using Task       = System.Threading.Tasks.Task;

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
    [RbacRule<V1Pod>(Verbs = RbacVerb.List, Scope = EntityScope.Cluster)]
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
            : base(name, version: KubeVersion.NeonKube)
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
            K8s = KubeHelper.CreateKubernetesClient();
            
            LogContext.SetCurrentLogProvider(TelemetryHub.LoggerFactory);

            if (NeonHelper.IsDevWorkstation)
            {
                this.PortForwardManager = new PortForwardManager(K8s, TelemetryHub.LoggerFactory);

                var headendTokenSecret = await K8s.CoreV1.ReadNamespacedSecretAsync("neoncloud-headend-token", KubeNamespace.NeonSystem);
                SetEnvironmentVariable("NEONCLOUD_HEADEND_TOKEN", Encoding.UTF8.GetString(headendTokenSecret.Data["token"]));
            }

            await WatchClusterInfoAsync();
            await ConfigureDexAsync();
            await ConfigureHarborAsync();

            HeadendClient = HeadendClient.Create();
            HeadendClient.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetEnvironmentVariable("NEONCLOUD_HEADEND_TOKEN"));

            // Start the web service.

            var operatorHost = KubernetesOperatorHost
               .CreateDefaultBuilder()
               .ConfigureOperator(settings =>
               {
                   settings.AssemblyScanningEnabled  = false;
                   settings.Name                     = Name;
                   settings.PodNamespace             = KubeNamespace.NeonSystem;
                   settings.UserImpersonationEnabled = false;
               })
               .ConfigureNeonKube()
               .AddSingleton(typeof(Service), this)
               .UseStartup<OperatorStartup>()
               .Build();

            _ = operatorHost.RunAsync();

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }

        /// <inheritdoc/>
        protected override bool OnLoggerConfg(OpenTelemetryLoggerOptions options)
        {
            if (DebugMode)
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
        /// Waits for the <see cref="ClusterInfo"/> object to be created for the cluster.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TimeoutException">Thrown if the cluster information could not be retrieved after a grace period.</exception>
        private async Task WatchClusterInfoAsync()
        {
            await SyncContext.Clear;

            // Start the watcher.

            // $todo(jefflill): This watcher should be disposed promptly.
            //
            //      https://github.com/nforgeio/operator-sdk/issues/26


            _ = K8s.WatchAsync<V1ConfigMap>(
                async (@event) =>
                {
                    await SyncContext.Clear;

                    ClusterInfo = Neon.K8s.TypedConfigMap<ClusterInfo>.From(@event.Value).Data;

                    Logger.LogInformationEx("Updated cluster info");
                },
                namespaceParameter: KubeNamespace.NeonStatus,
                fieldSelector:      $"metadata.name={KubeConfigMapName.ClusterInfo}",
                retryDelay:         TimeSpan.FromSeconds(30),
                logger:             Logger);

            // Wait for the watcher to discover the [ClusterInfo].

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
                var localPort = NetHelper.GetUnusedTcpPort(IPAddress.Loopback);

                try
                {
                    var pod = (await K8s.CoreV1.ListNamespacedPodAsync(KubeNamespace.NeonSystem, labelSelector: "app.kubernetes.io/name=dex")).Items.First();

                    PortForwardManager.StartPodPortForward(
                        name:       pod.Name(),
                        @namespace: KubeNamespace.NeonSystem,
                        localPort:  localPort,
                        remotePort: dexPort);
                }
                catch
                {
                }

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

            // $todo(jefflill): This watcher should be disposed promptly.
            //
            //      https://github.com/nforgeio/operator-sdk/issues/26
            //
            // Also: WHY ISN'T THIS BEING HANDLED BY A PROPER OPERATOR CONTROLLER???
            //       SEEMS LIKE A HACK!

            _ = K8s.WatchAsync<V1Secret>(
                async (@event) =>
                {
                    await SyncContext.Clear;

                    var sysadminUser = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(@event.Value.Data[KubeConst.SysAdminUser]));
                    var authString   = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sysadminUser.Name}:{sysadminUser.Password}"));

                    harborHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

                    Logger.LogInformationEx("Updated Harbor Client");
                },
                namespaceParameter: KubeNamespace.NeonSystem,
                fieldSelector:      $"metadata.name={KubeSecretName.GlauthUsers}",
                retryDelay:         TimeSpan.FromSeconds(30),
                logger:             Logger);
        }
    }
}
