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

using Neon.Common;
using Neon.IO;
using Neon.Hive;
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
            base.Initialize(controller);

            controller.AddStep(GetStepLabel("node config"), (node, stepDelay) => UpdateNode(node, stepDelay));
            controller.AddGlobalStep(GetStepLabel("neon-cluster-manager"), () => UpdateClusterManager());

            if (Cluster.Definition.Log.Enabled)
            {
                controller.AddGlobalStep(GetStepLabel("kibana dashboard"), () => UpdateKibanaDashboard());
            }

            controller.AddGlobalStep(GetStepLabel("cluster version"), () => UpdateClusterVersion());
        }

        /// <summary>
        /// Performs the cluster node updates.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        private void UpdateNode(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            Thread.Sleep(stepDelay);

            // [1.2.98] adds the [safe-apt-get] script that attempts to retry
            // when there are network problems.

            node.InvokeIdempotentAction(GetIdempotentTag("safe-apt-get"),
                () =>
                {
                    var targetPath = LinuxPath.Combine(HiveHostFolders.Tools, "safe-apt-get");

                    node.Status = "upload: safe-apt-get";
                    node.UploadText(targetPath, ResourceFiles.Root.GetFolder("Ubuntu-16.04").GetFolder("tools").GetFile("safe-apt-get.sh").Contents);
                    node.SudoCommand($"chmod 770 {targetPath}");
                });

            // Prevent the package manager from automatically updating Docker.

            node.InvokeIdempotentAction(GetIdempotentTag("disable-docker-auto-update"),
                () =>
                {
                    node.Status = "disable docker auto update";
                    node.SudoCommand("apt-mark hold docker");
                });

            // We need to install the [mmv] package so we can use it to easily
            // rename files.  We've also added this to the [setup-node] script
            // so it will be available for all new clusters going forward.

            node.InvokeIdempotentAction(GetIdempotentTag("install-mmv"),
                () =>
                {
                    node.Status = "install: mmv";
                    node.SudoCommand("safe-apt-get update");
                    node.SudoCommand("safe-apt-get install -yq mmv");
                });

            // Version 1.2.98 reorganizes the cluster nodes idempotent status directories.
            // Before this, we only tracked idempotency for cluster setup.  Starting
            // with 1.2.98, we're also allow for tracking cluster updates and perhaps
            // other types of operations in the future.
            //
            // We need to create a new [/var/local/neoncluster/setup] folder and relocate
            // the setup idempotent files names like [finished-*] there, and then
            // strip off the "finished-" prefix because we're no longer including that.

            node.InvokeIdempotentAction(GetIdempotentTag("relocate-setup-state"),
                () =>
                {
                    node.Status = "relocate setup state";

                    node.SudoCommand($"mkdir -p {HiveHostFolders.State}/setup");
                    node.SudoCommand($"mmv \"{HiveHostFolders.State}/finished-*\" \"{HiveHostFolders.State}/setup/#1*\"");
                });

            // The [/Ubuntu-16.04/updates/010297_010298.zip] resource file includes
            // the updated cluster setup scripts that need to be uploaded to all 
            // nodes.

            node.InvokeIdempotentAction(GetIdempotentTag("setup-files"),
                () =>
                {
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
                                node.UploadText(LinuxPath.Combine(HiveHostFolders.Setup, entry.Name), Encoding.UTF8.GetString(input.ReadToEnd()));
                            }
                        }
                    }

                    node.Status = string.Empty;
                });

            // We need to copy some systemd unit files into the drop-in folder so that
            // updating packages on the host won't blow away any customizations.  This
            // was really impacting Docker, which would restart outside of the swarm
            // with no containers after an update.  We'd probably see the same behavior
            // for other customized services.

            node.InvokeIdempotentAction(GetIdempotentTag("systemd-drop-ins"),
                () =>
                {
                    var unitFiles = new string[]
                    {
                        "ceph-mds@.service",
                        "ceph-mgr@.service",
                        "ceph-mgrs@.service",
                        "ceph-mon@.service",
                        "ceph-osd@.service",
                        "docker.service",
                        "openvpn@.service",
                        "vault.service"
                    };

                    foreach (var unitFile in unitFiles)
                    {
                        node.SudoCommand($"cp /lib/systemd/system/{unitFile} /etc/systemd/system{unitFile}");
                        node.SudoCommand($"chmod 644 /etc/systemd/system{unitFile}");
                    }

                    // Update systemd

                    node.SudoCommand("systemctl daemon-reload");
                });
        }

        /// <summary>
        /// Creates the [neon-ssh-credentials] secret and then updates [neon-cluster-manager] and
        /// its creation script on the managers to use the secret.
        /// </summary>
        private void UpdateClusterManager()
        {
            var firstManager = Cluster.FirstManager;

            // Create the [neon-ssh-credentials] secret because the new [neon-cluster-manager]
            // requires it.

            firstManager.InvokeIdempotentAction(GetIdempotentTag("neon-ssh-credentials"),
                () =>
                {
                    firstManager.Status = "secret: SSH credentials";
                    Cluster.Docker.Secret.Set("neon-ssh-credentials", $"{ClusterLogin.SshUsername}/{ClusterLogin.SshPassword}");
                });

            // Update the [neon-cluster-manager] service to the latest image and pass it the
            // new [neon-ssh-credentials] secret.

            firstManager.InvokeIdempotentAction(GetIdempotentTag("neon-cluster-manager"),
                () =>
                {
                    firstManager.Status = "update: neon-cluster-manager";
                    firstManager.SudoCommand($"docker service update --image {Program.ResolveDockerImage(Cluster.Definition.ClusterManagerImage)} --secret-add neon-ssh-credentials neon-cluster-manager");
                    firstManager.Status = string.Empty;
                });

            // Upload the new [neon-cluster-manager] service creation script to the managers.

            firstManager.InvokeIdempotentAction(GetIdempotentTag("neon-cluster-manager-script"),
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
                        manager.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-cluster-manager.sh"), createScript);
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

            firstManager.InvokeIdempotentAction(GetIdempotentTag("kibana-lb-rule"),
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
                            ProxyPort = HiveHostPorts.ProxyPrivateHttpKibana
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

            firstManager.InvokeIdempotentAction(GetIdempotentTag("kibana-dashboard"),
                () =>
                {
                    firstManager.Status = "kibana dashboard";

                    var kibanaDashboard = new ClusterDashboard()
                    {
                        Name        = "kibana",
                        Title       = "Kibana",
                        Folder      = HiveConst.DashboardSystemFolder,
                        Url         = $"http://healthy-manager:{HiveHostPorts.ProxyPrivateHttpKibana}",
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

            firstManager.InvokeIdempotentAction(GetIdempotentTag("cluster-version"),
                () =>
                {
                    firstManager.Status = "update: cluster version";
                    Cluster.Globals.Set(HiveGlobals.Version,(string)ToVersion);
                    firstManager.Status = string.Empty;
                });
        }
    }
}
