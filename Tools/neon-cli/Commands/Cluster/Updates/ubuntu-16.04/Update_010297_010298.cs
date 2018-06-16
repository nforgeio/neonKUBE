//-----------------------------------------------------------------------------
// FILE:	    Update_010297_010298.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Updates a cluster from version <b>1.2.97</b> to <b>1.2.98</b>.
    /// </summary>
    public class Update_010297_010298 : ClusterUpdate
    {
        /// <inheritdoc/>
        public override SemanticVersion FromVersion { get; protected set; } = SemanticVersion.Parse("1.2.97");

        /// <inheritdoc/>
        public override SemanticVersion ToVersion { get; protected set; } = SemanticVersion.Parse("1.2.98");

        /// <inheritdoc/>
        public override void AddUpdateSteps(SetupController<NodeDefinition> controller)
        {
            controller.AddStep("update: nodes", (node, stepDelay) => UpdateNode(node, stepDelay));
            controller.AddGlobalStep("update: neon-cluster-manager", () => UpdateClusterManager());

            if (Cluster.Definition.Log.Enabled)
            {
                controller.AddGlobalStep("update: kibana dashboard", () => UpdateKibanaDashboard());
            }

            controller.AddGlobalStep("update: cluster version", () => UpdateClusterVersion());
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Update [{FromVersion}] --> [{ToVersion}]";
        }

        /// <summary>
        /// Performs the cluster node updates.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        private void UpdateNode(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            Thread.Sleep(stepDelay);

            // We need to install the [mmv] package so we can use it to easily
            // rename files.  We've also added this to the [setup-node] script
            // so it will be available for all new clusters going forward.

            node.InvokeIdempotentAction("install-mmv",
                () =>
                {
                    node.Status = "install mmv";

                    node.SudoCommand("apt-get update");
                    node.SudoCommand("apt-get install -yq mmv");
                });

            // Version 1.2.98 reorganizes the cluster nodes itempotent status directories.
            // Before this, we only tracked idempotency for cluster setup.  Starting
            // with 1.2.98, we're also allow for tracking cluster updates and perhaps
            // other types of operations in the future.
            //
            // We need to create a new [/var/local/neoncluster/setup] folder and relocate
            // the setup idempotent files names like [finished-*] there, and then
            // strip off the "finished-" prefix because we're no longer including that.

            node.InvokeIdempotentAction(GetItempotentTag("relocate-setup-state"),
                () =>
                {
                    node.Status = "relocate setup state";

                    node.SudoCommand($"mkdir -p {NeonHostFolders.State}/setup");
                    node.SudoCommand($"mmv \"{NeonHostFolders.State}/finished-*\" \"{NeonHostFolders.State}/setup/#1*\"");
                });

            // The [/Ubuntu-16.04/updates/010297_010298.zip] resource file includes
            // the updated cluster setup scripts that need to be uploaded to all 
            // nodes.

            node.Status = "update setup scripts";

            using (var zip = new ZipFile(ResourceFiles.Root.GetFolder("Ubuntu-16.04").GetFolder("updates").GetFile("010297_010298.zip").Path))
            {
                foreach (ZipEntry entry in zip)
                {
                    if (!entry.IsFile)
                    {
                        continue;   // Not expecting any subdirectories, etc.
                    }

                    using (var input = zip.GetInputStream(entry))
                    {
                        node.Status = $"update: {entry.Name}";
                        node.UploadText(LinuxPath.Combine(NeonHostFolders.Setup, entry.Name), Encoding.UTF8.GetString(input.ReadToEnd()));
                    }
                }
            }

            node.Status = string.Empty;
        }

        /// <summary>
        /// Creates the [neon-ssh-credentials] secret and then updates [neon-cluster-manager] and
        /// its create script on the managers to use the secret.
        /// </summary>
        private void UpdateClusterManager()
        {
            var firstManager = Cluster.FirstManager;

            // Create the [neon-ssh-credentials] secret because the new [neon-cluster-manager]
            // requires it.

            firstManager.InvokeIdempotentAction(GetItempotentTag("neon-ssh-credentials"),
                () =>
                {
                    firstManager.Status = "SSH credentials secret";
                    Cluster.DockerSecret.Set("neon-ssh-credentials", $"{ClusterLogin.SshUsername}/{ClusterLogin.SshPassword}");
                });

            // Update the [neon-cluster-manager] service to the latest image and pass it the
            // new [neon-ssh-credentials] secret.

            firstManager.InvokeIdempotentAction(GetItempotentTag("neon-cluster-manager"),
                () =>
                {
                    firstManager.Status = "update: neon-cluster-manager";
                    firstManager.SudoCommand($"docker service update --image {Program.ResolveDockerImage(Cluster.Definition.ClusterManagerImage)} --secret-add neon-ssh-credentials neon-cluster-manager");
                    firstManager.Status = string.Empty;
                });

            // Upload the new [neon-cluster-manager] service creation script to the managers.

            firstManager.InvokeIdempotentAction(GetItempotentTag("neon-cluster-manager-script"),
                () =>
                {
                    string unsealSecretOption = null;

                    if (Cluster.Definition.Vault.AutoUnseal)
                    {
                        unsealSecretOption = "--secret=neon-cluster-manager-vaultkeys";
                    }

                    var bundle = new CommandBundle(
                        "docker service create",
                        "--name", "neon-cluster-manager",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                        "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                        "--mount", "type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock",
                        "--env", "LOG_LEVEL=INFO",
                        "--secret", "neon-ssh-credentials",
                        unsealSecretOption,
                        "--constraint", "node.role==manager",
                        "--replicas", 1,
                        "--restart-delay", Cluster.Definition.Docker.RestartDelay,
                        Program.ResolveDockerImage(Cluster.Definition.ClusterManagerImage));

                    var createScript = bundle.ToBash();

                    foreach (var manager in Cluster.Managers)
                    {
                        manager.Status = "update: neon-cluster-manager script";
                        manager.UploadText(LinuxPath.Combine(NeonHostFolders.Scripts, "neon-cluster-manager.sh"), createScript);
                        manager.Status = string.Empty;
                    }
                });
        }

        /// <summary>
        /// Updates the Kibana dashboard so that it's running behind a load balancer rule.
        /// </summary>
        private void UpdateKibanaDashboard()
        {
            var firstManager = Cluster.FirstManager;

            // Create the [neon-ssh-credentials] secret because the new [neon-cluster-manager]
            // requires it.

            firstManager.InvokeIdempotentAction(GetItempotentTag("kibana-lb-rule"),
                () =>
                {
                    firstManager.Status = "kibana load balancer rule";

                    var rule = new LoadBalancerHttpRule()
                    {
                        Name     = "neon-log-kibana",
                        System   = true,
                        Log      = true,
                        Resolver = null
                    };

                    rule.Frontends.Add(
                        new LoadBalancerHttpFrontend()
                        {
                            ProxyPort = NeonHostPorts.ProxyPrivateHttpKibana
                        });

                    rule.Backends.Add(
                        new LoadBalancerHttpBackend()
                        {
                            Server = "neon-log-kibana",
                            Port   = NetworkPorts.Kibana
                        });

                    Cluster.PrivateLoadBalancer.SetRule(rule);

                    firstManager.Status = string.Empty;
                });

            // Update the Kibana dashboard to use the new load balancer rule.

            firstManager.InvokeIdempotentAction(GetItempotentTag("kibana-dashboard"),
                () =>
                {
                    firstManager.Status = "kibana dashboard";

                    var kibanaDashboard = new ClusterDashboard()
                    {
                        Name        = "kibana",
                        Title       = "Kibana",
                        Folder      = NeonClusterConst.DashboardSystemFolder,
                        Url         = $"http://healthy-manager:{NeonHostPorts.ProxyPrivateHttpKibana}",
                        Description = "Kibana cluster monitoring dashboard"
                    };

                    Cluster.Dashboard.Set(kibanaDashboard);
                    firstManager.Status = string.Empty;
                });
        }

        /// <summary>
        /// Updates the cluster version.
        /// </summary>
        private void UpdateClusterVersion()
        {
            var firstManager = Cluster.FirstManager;

            firstManager.InvokeIdempotentAction(GetItempotentTag("cluster-version"),
                () =>
                {
                    Cluster.Globals.Set(NeonClusterGlobals.NeonCliVersion,(string)ToVersion);
                });
        }
    }
}
