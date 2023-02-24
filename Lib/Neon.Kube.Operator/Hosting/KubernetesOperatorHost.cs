//-----------------------------------------------------------------------------
// FILE:	    KubernetesOperatorHost.cs
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
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Operator.Builder;
using Neon.Kube.Operator.Commands.Generate;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Resources.CertManager;
using Neon.Tasks;

using k8s;
using k8s.Models;

using Prometheus;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Kubernetes operator Host.
    /// </summary>
    public class KubernetesOperatorHost : IKubernetesOperatorHost
    {
        //---------------------------------------------------------------------
        // Static members

        /// <inheritdoc/>
        public static IKubernetesOperatorHostBuilder CreateDefaultBuilder(string[] args = null)
        {
            return new KubernetesOperatorHostBuilder(args);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string[]    args;
        private ILogger     logger;
        private IKubernetes k8s;

        /// <summary>
        /// Consructor.
        /// </summary>
        /// <param name="args">The operator's command line arguments.</param>
        public KubernetesOperatorHost(string[] args = null)
        {
            this.args = args;
        }

        /// <inheritdoc/>
        public IWebHost Host { get; set; }

        /// <inheritdoc/>
        public IWebHostBuilder HostBuilder { get; set; }

        /// <inheritdoc/>
        public CertManagerOptions CertManagerOptions { get; set; }

        /// <inheritdoc/>
        public OperatorSettings OperatorSettings { get; set; }

        /// <inheritdoc/>
        public Type StartupType { get; set; }

        /// <inheritdoc/>
        public X509Certificate2 Certificate { get; set; }

        /// <inheritdoc/>
        public void Run()
        {
            if (CertManagerOptions != null)
            {
                OperatorSettings.certManagerEnabled = true;
            }

            if (args == null || args?.Count() == 0)
            {
                Host   = HostBuilder.Build();
                logger = Host.Services.GetService<ILoggerFactory>()?.CreateLogger<KubernetesOperatorHost>();

                if (NeonHelper.IsDevWorkstation || Debugger.IsAttached)
                {
                    k8s = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig(), new KubernetesRetryHandler());

                    ConfigureRbacAsync().RunSynchronously();
                }

                k8s = Host.Services.GetRequiredService<IKubernetes>();

                if (OperatorSettings.certManagerEnabled)
                {
                    CheckCertificateAsync().RunSynchronously();
                }

                Host.Start();

                return;
            }

            HostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<GenerateCommand>();
                services.AddSingleton<GenerateCommandBase, GenerateRbacCommand>();
            });

            Host = HostBuilder.Build();

            // Build the commands from what's registered in the DI container.

            var rootCommand = new RootCommand();

            foreach (Command command in Host.Services.GetServices<GenerateCommand>())
            {
                rootCommand.AddCommand(command);
            }

            var generateCommand = Host.Services.GetService<GenerateCommand>();

            foreach (Command command in Host.Services.GetServices<GenerateCommandBase>())
            {
                generateCommand.AddCommand(command);
            }

            var commandLineBuilder = new CommandLineBuilder(rootCommand);
            var parser             = commandLineBuilder.UseDefaults().Build();

            // Invoke the command line parser which then invokes the respective command handlers.

            parser.Invoke(args);
        }

        /// <inheritdoc/>
        public async Task RunAsync()
        {
            OperatorSettings = OperatorSettings ?? new OperatorSettings();

            if (CertManagerOptions != null)
            {
                OperatorSettings.certManagerEnabled = true;
            }

            if (args == null || args?.Count() == 0) 
            {
                Host   = HostBuilder.Build();
                logger = Host.Services.GetService<ILoggerFactory>()?.CreateLogger<KubernetesOperatorHost>();

                if (NeonHelper.IsDevWorkstation || Debugger.IsAttached)
                {
                    k8s = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig(),new KubernetesRetryHandler());

                    await ConfigureRbacAsync();
                }

                k8s    = Host.Services.GetRequiredService<IKubernetes>();

                if (OperatorSettings.certManagerEnabled)
                {
                    await CheckCertificateAsync();
                }

                await Host.RunAsync();

                return;
            }

            HostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<GenerateCommand>();
                services.AddSingleton<GenerateCommandBase, GenerateRbacCommand>();
            });

            Host = HostBuilder.Build();

            // Build the commands from what's registered in the DI container.

            var rootCommand = new RootCommand();

            foreach (Command command in Host.Services.GetServices<GenerateCommand>())
            {
                rootCommand.AddCommand(command);
            }

            var generateCommand = Host.Services.GetService<GenerateCommand>();

            foreach (Command command in Host.Services.GetServices<GenerateCommandBase>())
            {
                generateCommand.AddCommand(command);
            }

            var commandLineBuilder = new CommandLineBuilder(rootCommand);
            var parser             = commandLineBuilder.UseDefaults().Build();

            // Invoke the command line parser which then invokes the respective command handlers.

            await parser.InvokeAsync(args);

            return;
        }

        private async Task CheckCertificateAsync()
        {
            using var activity = TraceContext.ActivitySource?.StartActivity();

            logger?.LogInformationEx(() => "Checking webhook certificate.");

            var cert = await k8s.CustomObjects.ListNamespacedCustomObjectAsync<V1Certificate>(OperatorSettings.DeployedNamespace, labelSelector: $"{NeonLabel.ManagedBy}={OperatorSettings.Name}");

            if (!cert.Items.Any())
            {
                logger?.LogInformationEx(() => "Webhook certificate does not exist, creating...");

                var certificate = new V1Certificate()
                {
                    Metadata = new V1ObjectMeta()
                    {
                        Name              = OperatorSettings.Name,
                        NamespaceProperty = OperatorSettings.DeployedNamespace,
                        Labels            = new Dictionary<string, string>()
                        {
                            { NeonLabel.ManagedBy, OperatorSettings.Name }
                        }
                    },
                    Spec = new V1CertificateSpec()
                    {
                        DnsNames = new List<string>()
                        {
                            $"{OperatorSettings.Name}",
                            $"{OperatorSettings.Name}.{OperatorSettings.DeployedNamespace}",
                            $"{OperatorSettings.Name}.{OperatorSettings.DeployedNamespace}.svc",
                            $"{OperatorSettings.Name}.{OperatorSettings.DeployedNamespace}.svc.cluster.local",
                        },
                        Duration   = $"{CertManagerOptions.CertificateDuration.TotalHours}h{CertManagerOptions.CertificateDuration.Minutes}m{CertManagerOptions.CertificateDuration.Seconds}s",
                        IssuerRef  = CertManagerOptions.IssuerRef,
                        SecretName = $"{OperatorSettings.Name}-webhook-tls"
                    }
                };

                await k8s.CustomObjects.UpsertNamespacedCustomObjectAsync(certificate, certificate.Namespace(), certificate.Name());

                logger?.LogInformationEx(() => "Webhook certificate created.");
            }

            _ = k8s.WatchAsync<V1Secret>(
                async (@event) =>
                {
                    await SyncContext.Clear;

                    using var activity = TraceContext.ActivitySource?.StartActivity("UpdatingWebhookCertificate");

                    Certificate = X509Certificate2.CreateFromPem(
                        Encoding.UTF8.GetString(@event.Value.Data["tls.crt"]),
                        Encoding.UTF8.GetString(@event.Value.Data["tls.key"]));

                    logger?.LogInformationEx("Updated webhook certificate");
                },
                OperatorSettings.DeployedNamespace,
                fieldSelector: $"metadata.name={OperatorSettings.Name}-webhook-tls");

            using (TraceContext.ActivitySource?.StartActivity("WaitForSecret", ActivityKind.Internal))
            {
                await NeonHelper.WaitForAsync(
                    async () =>
                    {
                        await SyncContext.Clear;

                        if (Certificate != null)
                        {
                            logger?.LogInformationEx(() => "Cert updated");

                            return true;
                        }

                        return false;
                    },
                    timeout:      TimeSpan.FromSeconds(300),
                    pollInterval: TimeSpan.FromMilliseconds(500));
            }
        }

        private async Task ConfigureRbacAsync()
        {
            var rbac = new RbacBuilder(Host.Services, @namespace: OperatorSettings.DeployedNamespace);

            rbac.Build();

            foreach (var sa in rbac.ServiceAccounts)
            {
                logger?.LogInformationEx(() => $"Creating ServiceAccount {sa.Namespace()}/{sa.Name()}.");

                var serviceAccounts = await k8s.CoreV1.ListNamespacedServiceAccountAsync(sa.Namespace(), fieldSelector: $"metadata.name={sa.Name()}");
                
                if (serviceAccounts.Items.Any())
                {
                    await k8s.CoreV1.DeleteNamespacedServiceAccountAsync(sa.Name(), sa.Namespace());
                }

                await k8s.CoreV1.CreateNamespacedServiceAccountAsync(sa, sa.Namespace());
            }

            foreach (var cr in rbac.ClusterRoles)
            {
                logger?.LogInformationEx(() => $"Creating ClusterRole {cr.Name()}.");

                var clusterRoles = await k8s.RbacAuthorizationV1.ListClusterRoleAsync(fieldSelector: $"metadata.name={cr.Name()}");

                if (clusterRoles.Items.Any())
                {
                    await k8s.RbacAuthorizationV1.DeleteClusterRoleAsync(cr.Name());
                }

                await k8s.RbacAuthorizationV1.CreateClusterRoleAsync(cr);
            }

            foreach (var crb in rbac.ClusterRoleBindings)
            {
                logger?.LogInformationEx(() => $"Creating ClusterRoleBinding {crb.Name()}.");

                var clusterRoleBindings = await k8s.RbacAuthorizationV1.ListClusterRoleBindingAsync(fieldSelector: $"metadata.name={crb.Name()}");

                if (clusterRoleBindings.Items.Any())
                {
                    await k8s.RbacAuthorizationV1.DeleteClusterRoleBindingAsync(crb.Name());
                }

                await k8s.RbacAuthorizationV1.CreateClusterRoleBindingAsync(crb);
            }

            foreach (var r in rbac.Roles)
            {
                logger?.LogInformationEx(() => $"Creating Role {r.Namespace()}/{r.Name()}.");

                var roles = await k8s.RbacAuthorizationV1.ListNamespacedRoleAsync(r.Namespace(), fieldSelector: $"metadata.name={r.Name()}");

                if (roles.Items.Any())
                {
                    await k8s.RbacAuthorizationV1.DeleteNamespacedRoleAsync(r.Name(), r.Namespace());
                }

                await k8s.RbacAuthorizationV1.CreateNamespacedRoleAsync(r, r.Namespace());
            }

            foreach (var rb in rbac.RoleBindings)
            {
                logger?.LogInformationEx(() => $"Creating RoleBinding {rb.Namespace()}/{rb.Name()}.");

                var roleBindings = await k8s.RbacAuthorizationV1.ListNamespacedRoleBindingAsync(rb.Namespace(), fieldSelector: $"metadata.name={rb.Name()}");

                if (roleBindings.Items.Any())
                {
                    await k8s.RbacAuthorizationV1.DeleteNamespacedRoleBindingAsync(rb.Name(), rb.Namespace());
                }

                await k8s.RbacAuthorizationV1.CreateNamespacedRoleBindingAsync(rb, rb.Namespace());
            }
        }
    }
}
